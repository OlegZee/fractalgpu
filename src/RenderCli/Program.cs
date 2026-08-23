using System.CommandLine;

using FractalGpu.Rendering.Common;
using FractalGpu.Rendering.Fractal;

void Render(LyapRendererBase renderer, string? fileName)
{
    var picSize = 256;
    var iterations = 10000;

    var settings = new Lyapunov.Settings
    {
        A = new Range<double>(2, 4),
        B = new Range<double>(2, 4),
        Pattern = "ab",
        InitialValue = 0.5,
        Warmup = iterations / 10,
        Iterations = iterations,
        Size = new Sz(picSize, picSize),
        Contrast = 1.7,
    };

    var startTime = DateTime.Now;
    var bmp = renderer.Render(settings);

    var execTime = DateTime.Now - startTime;
    var perf = settings.Size.Width * settings.Size.Height * settings.Iterations/1024/1024/execTime.TotalSeconds;

    Console.WriteLine(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
        execTime.TotalSeconds, settings.Pattern, settings.Iterations,
        settings.Size.Width, settings.Size.Height, renderer, perf));

    if(!string.IsNullOrEmpty(fileName)) bmp.Save(fileName);
}

BenchResult Benchmark(DeviceDescriptor device)
{
    var renderer = device.CreateRenderer();
    var picSize = 256;
    var numIterations = 1000;

    var settings = new Lyapunov.Settings
    {
        A = new Range<double>(2, 4),
        B = new Range<double>(2, 4),
        Pattern = "ab",
        InitialValue = 0.5,
        Contrast = 1.7,
    };

    var steps = new[]
    {
        () => { picSize = 256; numIterations = 1000; },
        () => { picSize = 512; },
        () => { picSize = 1024; },
        () => { numIterations = 2500; },
        () => { numIterations = 5000; },
        () => { numIterations = 10000; },
        () => { numIterations = 25000; },
        () => { numIterations = 50000; },
        () => { picSize = 1536; },
        () => { picSize = 2048; },
        () => { picSize = 4096; },
    };

    var peakMis = 0.0;
    var peakSize = new Sz(0, 0);
    var peakIterations = 0;
    var totalTime = TimeSpan.Zero;

    TimeSpan execTime;
    var stepIndex = 0;
    do
    {
        steps[stepIndex]();
        settings = settings with { Warmup = numIterations / 10, Iterations = numIterations, Size = new Sz(picSize, picSize) };

        var startTime = DateTime.Now;
        var bmp = renderer.Render(settings);

        execTime = DateTime.Now - startTime;
        var perf = settings.Size.Width * settings.Size.Height * settings.Iterations / 1024 / 1024 /
                   execTime.TotalSeconds;

        Console.WriteLine(string.Format("Rendering time: {0:#0.000}s {6:#0.##}mis '{1}' N{2} {3}x{4} @{5}",
            execTime.TotalSeconds, settings.Pattern, settings.Iterations,
            settings.Size.Width, settings.Size.Height, renderer, perf));

        totalTime += execTime;
        if (perf > peakMis) { peakMis = perf; peakSize = settings.Size; peakIterations = settings.Iterations; }

        stepIndex++;
    } while (execTime.TotalSeconds < 2.5 && stepIndex < steps.Length);

    return new BenchResult(device, peakMis, peakSize, peakIterations, totalTime);
}

void PrintDeviceTable()
{
    var devices = DeviceRegistry.Enumerate(out var openClError);

    foreach (var device in devices)
    {
        var line = $"[{device.Index}] {device.Name}";
        if (!string.IsNullOrEmpty(device.Details)) line += "  " + device.Details;
        Console.WriteLine(line);
    }

    if (openClError != null)
        Console.WriteLine($"OpenCL enumeration failed: {openClError} (CPU devices still available)");
}

var deviceOption = new Option<int[]>("--device", "-d")
{
    Description = "Device index from 'list-devices'; repeatable (-d 0 -d 2) or space-separated (-d 0 2). Default: all devices",
    AllowMultipleArgumentsPerToken = true,
};

var benchmarkCommand = new Command("benchmark", "Run the escalating render benchmark on a selected device");
benchmarkCommand.Options.Add(deviceOption);
benchmarkCommand.SetAction(parseResult =>
{
    var requested = parseResult.GetValue(deviceOption) ?? [];

    List<DeviceDescriptor> devices;
    if (requested.Length == 0)
    {
        devices = DeviceRegistry.Enumerate(out var openClError).ToList();
        if (openClError != null)
            Console.WriteLine($"OpenCL enumeration failed: {openClError} (CPU devices still available)");
    }
    else
    {
        devices = [];
        foreach (var index in requested.Distinct())
        {
            try { devices.Add(DeviceRegistry.GetByIndex(index)); }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message} Run 'list-devices' to see available devices.");
                return 1;
            }
        }
    }

    var results = new List<BenchResult>();
    var anyFailed = false;
    foreach (var device in devices)
    {
        Console.WriteLine($"fractalgpu benchmark on [{device.Index}] {device.Name}");
        if (!string.IsNullOrEmpty(device.Details)) Console.WriteLine($"  {device.Details}");
        try
        {
            var result = Benchmark(device);
            Console.WriteLine($"Best: {result.PeakMis:#0.##}mis at {result.PeakSize.Width}x{result.PeakSize.Height} N{result.PeakIterations} (total {result.TotalTime.TotalSeconds:#0.0}s)");
            results.Add(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            anyFailed = true;
        }
        Console.WriteLine();
    }

    if (results.Count > 1)
    {
        Console.WriteLine("Summary (peak throughput):");
        foreach (var r in results)
            Console.WriteLine($"  [{r.Device.Index}] {r.Device.Name}: {r.PeakMis:#0.##}mis ({r.PeakSize.Width}x{r.PeakSize.Height} N{r.PeakIterations})");
    }

    return anyFailed ? 1 : 0;
});

var listDevicesCommand = new Command("list-devices", "List all available render devices (CPU modes and OpenCL devices) with their indexes");
listDevicesCommand.SetAction(_ =>
{
    PrintDeviceTable();
    return 0;
});

var rootCommand = new RootCommand("FractalGPU RenderCli — Lyapunov fractal rendering and benchmarking");
rootCommand.Subcommands.Add(benchmarkCommand);
rootCommand.Subcommands.Add(listDevicesCommand);
rootCommand.SetAction(_ =>
{
    rootCommand.Parse("--help").Invoke();
    return 0;
});

return rootCommand.Parse(args).Invoke();

internal sealed record BenchResult(DeviceDescriptor Device, double PeakMis, Sz PeakSize, int PeakIterations, TimeSpan TotalTime);
