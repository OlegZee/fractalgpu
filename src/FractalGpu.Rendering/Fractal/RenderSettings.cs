using FractalGpu.Rendering.Common;

namespace FractalGpu.Rendering.Fractal
{
    /// <summary>
    /// Fractal rendering settings
    /// </summary>
    public abstract record RenderSettings
    {
        public Sz Size { get; init; }
    }
}
