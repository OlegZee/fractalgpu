using System.Diagnostics;
using FractalGpu.Rendering.Common;
using FractalGpu.Rendering.Fractal;

namespace FractalGpu.Rendering.Benchmarking
{
    /// <summary>One step of the escalating benchmark: what was rendered and how fast.</summary>
    public sealed record BenchStep(Lyapunov.Settings Settings, TimeSpan Elapsed, double Mis);

    /// <summary>Outcome of a benchmark run on a single device.</summary>
    public sealed record BenchResult(DeviceDescriptor Device, double PeakMis, Sz PeakSize, int PeakIterations, TimeSpan TotalTime, IReadOnlyList<BenchStep> Steps);

    /// <summary>
    /// The escalating render benchmark shared by RenderCli and the mobile bench apps.
    /// Throughput is reported in "mis" (mega-iterations per second: pixels x iterations / 2^20 / seconds).
    /// </summary>
    public static class Benchmark
    {
        /// <summary>The run stops after the first step that takes at least this long.</summary>
        public const double StopThresholdSeconds = 2.5;

        private static readonly Lyapunov.Settings BaseSettings = new()
        {
            A = new Range<double>(2, 4),
            B = new Range<double>(2, 4),
            Pattern = "ab",
            InitialValue = 0.5,
            Contrast = 1.7,
        };

        // (size, iterations) escalation ladder; warmup is always iterations / 10
        private static readonly (int Size, int Iterations)[] Ladder =
        [
            (256, 1000),
            (512, 1000),
            (1024, 1000),
            (1024, 2500),
            (1024, 5000),
            (1024, 10000),
            (1024, 25000),
            (1024, 50000),
            (1536, 50000),
            (2048, 50000),
            (4096, 50000),
        ];

        /// <summary>
        /// Runs the escalating benchmark on <paramref name="device"/>.
        /// <paramref name="log"/> receives one human-readable line per step (the same text RenderCli prints).
        /// </summary>
        public static BenchResult Run(DeviceDescriptor device, Action<string>? log = null)
        {
            var renderer = device.CreateRenderer();
            var steps = new List<BenchStep>();

            var peakMis = 0.0;
            var peakSize = new Sz(0, 0);
            var peakIterations = 0;
            var totalTime = TimeSpan.Zero;

            foreach (var (size, iterations) in Ladder)
            {
                var settings = BaseSettings with
                {
                    Warmup = iterations / 10,
                    Iterations = iterations,
                    Size = new Sz(size, size),
                };

                var stopwatch = Stopwatch.StartNew();
                renderer.Render(settings);
                var elapsed = stopwatch.Elapsed;

                var mis = settings.Size.Width * settings.Size.Height * settings.Iterations / 1024 / 1024 / elapsed.TotalSeconds;
                var step = new BenchStep(settings, elapsed, mis);
                steps.Add(step);
                log?.Invoke(FormatStep(step, renderer));

                totalTime += elapsed;
                if (mis > peakMis) { peakMis = mis; peakSize = settings.Size; peakIterations = settings.Iterations; }

                if (elapsed.TotalSeconds >= StopThresholdSeconds) break;
            }

            return new BenchResult(device, peakMis, peakSize, peakIterations, totalTime, steps);
        }

        public static string FormatStep(BenchStep step, object renderer)
        {
            var s = step.Settings;
            return string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
                step.Elapsed.TotalSeconds, s.Pattern, s.Iterations, s.Size.Width, s.Size.Height, renderer, step.Mis);
        }

        public static string FormatBest(BenchResult result) =>
            $"Best: {result.PeakMis:#0.##}mis at {result.PeakSize.Width}x{result.PeakSize.Height} N{result.PeakIterations} (total {result.TotalTime.TotalSeconds:#0.0}s)";

        /// <summary>
        /// Peak-throughput comparison table. The speedup column is relative to the single-core CPU
        /// baseline when it was benchmarked, otherwise to the slowest device in the run.
        /// </summary>
        public static IEnumerable<string> FormatSummary(IReadOnlyList<BenchResult> results)
        {
            if (results.Count == 0) yield break;

            var baseline = results.FirstOrDefault(r => r.Device.Kind == DeviceKind.Cpu)
                           ?? results.MinBy(r => r.PeakMis)!;
            var nameWidth = results.Max(r => r.Device.Name.Length + $"[{r.Device.Index}] ".Length);

            yield return "Summary (peak throughput):";
            yield return $"  {"Device".PadRight(nameWidth)}  {"Peak mis",12}  {"At",-16}  {$"x vs [{baseline.Device.Index}]",10}";
            foreach (var r in results.OrderBy(r => r.PeakMis))
            {
                var speedup = r.PeakMis / baseline.PeakMis;
                yield return
                    $"  {$"[{r.Device.Index}] {r.Device.Name}".PadRight(nameWidth)}  {r.PeakMis,12:#,0.0}  {$"{r.PeakSize.Width}x{r.PeakSize.Height} N{r.PeakIterations}",-16}  {speedup,9:#,0.0}x";
            }
        }
    }
}
