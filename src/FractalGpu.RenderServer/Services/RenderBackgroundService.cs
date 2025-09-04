using FractalGpu.Rendering.Fractal;
using FractalGpu.RenderServer.Models;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

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

        // Setup OpenCL environment for macOS if needed
        SetupOpenClEnvironment();
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
        try
        {
            // Try GPU first
            return new LyapRendererOpenCl();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GPU renderer failed to initialize, falling back to CPU");

            try
            {
                // Try multicore CPU
                return new LyapRendererMulticore<LyapRendererCpu>(Environment.ProcessorCount);
            }
            catch (Exception ex2)
            {
                _logger.LogWarning(ex2, "Multicore CPU renderer failed, using single-core CPU");

                // Fallback to single-core CPU
                return new LyapRendererCpu();
            }
        }
    }

    private void SetupOpenClEnvironment()
    {
        // Setup OpenCL environment for macOS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var path = Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH");
            if (string.IsNullOrEmpty(path) || !path.Contains("OpenCL.framework"))
            {
                var newPath = path + ":/System/Library/Frameworks/OpenCL.framework";
                Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", newPath);
                _logger.LogInformation("Set DYLD_LIBRARY_PATH for OpenCL on macOS");
            }
        }
    }

    public override void Dispose()
    {
        _parallelismSemaphore?.Dispose();
        base.Dispose();
    }
}