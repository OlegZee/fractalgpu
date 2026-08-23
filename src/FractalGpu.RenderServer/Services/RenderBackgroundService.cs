using FractalGpu.Rendering.Fractal;
using FractalGpu.RenderServer.Models;
using Microsoft.Extensions.Options;

namespace FractalGpu.RenderServer.Services;

public class RenderBackgroundService : BackgroundService
{
    private readonly RenderQueue _renderQueue;
    private readonly QueueSettings _queueSettings;
    private readonly ILogger<RenderBackgroundService> _logger;
    private readonly SemaphoreSlim _parallelismSemaphore;

    public RenderBackgroundService(
        IRenderQueue renderQueue,
        IOptions<QueueSettings> queueSettings,
        ILogger<RenderBackgroundService> logger)
    {
        _renderQueue = (RenderQueue)renderQueue;
        _queueSettings = queueSettings.Value;
        _logger = logger;
        _parallelismSemaphore = new SemaphoreSlim(_queueSettings.MaxParallelJobs, _queueSettings.MaxParallelJobs);

        DeviceRegistry.Enumerate(out var openClError);
        if (openClError != null)
            _logger.LogWarning("OpenCL enumeration failed: {Error} (CPU devices still available)", openClError);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RenderBackgroundService started with {MaxParallelJobs} parallel jobs",
            _queueSettings.MaxParallelJobs);

        try
        {
            await foreach (var job in _renderQueue.Reader.ReadAllAsync(stoppingToken))
            {
                // Process jobs with limited parallelism
                _ = Task.Run(async () =>
                {
                    await _parallelismSemaphore.WaitAsync(stoppingToken);
                    try
                    {
                        await ProcessRenderJob(job, stoppingToken);
                    }
                    finally
                    {
                        _parallelismSemaphore.Release();
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RenderBackgroundService stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in RenderBackgroundService");
            throw;
        }
    }

    private async Task ProcessRenderJob(QueuedRenderJob job, CancellationToken cancellationToken)
    {
        _renderQueue.IncrementActiveJobs();
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting render job: {Width}x{Height}, Pattern: {Pattern}, Iterations: {Iterations}",
                job.Settings.Size.Width, job.Settings.Size.Height, job.Settings.Pattern, job.Settings.Iterations);

            // Create appropriate renderer (prefer GPU, fallback to CPU)
            LyapRendererBase renderer = CreateRenderer();

            // Perform the rendering
            var result = await Task.Run(() => renderer.Render(job.Settings), cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Completed render job in {Duration:F2}s: {Width}x{Height}",
                duration.TotalSeconds, job.Settings.Size.Width, job.Settings.Size.Height);

            // Complete the task
            job.CompletionSource.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Render job cancelled: {Width}x{Height}",
                job.Settings.Size.Width, job.Settings.Size.Height);
            job.CompletionSource.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Render job failed after {Duration:F2}s: {Width}x{Height}",
                duration.TotalSeconds, job.Settings.Size.Width, job.Settings.Size.Height);
            job.CompletionSource.TrySetException(ex);
        }
        finally
        {
            _renderQueue.DecrementActiveJobs();
        }
    }

    private LyapRendererBase CreateRenderer()
    {
        var device = DeviceRegistry.GetByIndex(DeviceRegistry.DefaultIndex());
        _logger.LogInformation("Rendering on device [{Index}] {Name}", device.Index, device.Name);
        return device.CreateRenderer();
    }

    public override void Dispose()
    {
        _parallelismSemaphore?.Dispose();
        base.Dispose();
    }
}
