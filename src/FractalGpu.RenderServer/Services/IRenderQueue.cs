using FractalGpu.Rendering.Fractal;
using FractalGpu.Rendering.Media;
using FractalGpu.RenderServer.Models;

namespace FractalGpu.RenderServer.Services;

public interface IRenderQueue
{
    /// <summary>
    /// Queue a render job and wait for completion
    /// </summary>
    /// <param name="settings">Fractal rendering settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered bitmap</returns>
    Task<RawBitmap> QueueRenderAsync(Lyapunov.Settings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current queue status
    /// </summary>
    /// <returns>Queue status information</returns>
    QueueStatus GetQueueStatus();
}