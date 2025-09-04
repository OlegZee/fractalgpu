using FractalGpu.Rendering.Fractal;
using FractalGpu.Rendering.Media;

namespace FractalGpu.RenderServer.Models;

public class QueuedRenderJob
{
    public Lyapunov.Settings Settings { get; set; } = null!;
    public TaskCompletionSource<RawBitmap> CompletionSource { get; set; } = null!;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}