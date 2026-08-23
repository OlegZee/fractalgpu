using FractalGpu.Rendering.Common;

namespace FractalGpu.Rendering.Fractal
{
    public static class Lyapunov
    {
        /// <summary>
        /// Defines fractal settings
        /// </summary>
        public sealed record Settings : RenderSettings
        {
            public double InitialValue { get; init; } = 0.5;
            public Range<double> A { get; init; } = new(1, 4);
            public Range<double> B { get; init; } = new(1, 4);
            public string Pattern { get; init; } = "ab";

            // View settings
            public double Contrast { get; init; } = 2;

            public int Warmup { get; init; } = 10;
            public int Iterations { get; init; } = 100;

            // TODO validation
            // TODO pattern as bool array
        }
    }
}
