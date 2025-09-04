using System.Threading.Channels;
using FractalGpu.Rendering.Fractal;
using FractalGpu.Rendering.Media;
using FractalGpu.RenderServer.Models;
using Microsoft.Extensions.Options;

namespace FractalGpu.RenderServer.Services;

public class RenderQueue : IRenderQueue
{
    private readonly Channel<QueuedRenderJob> _queue;
    private readonly QueueSettings _settings;
    private readonly ILogger<RenderQueue> _logger;
    private int _activeJobs = 0;

    public RenderQueue(IOptions<QueueSettings> settings, ILogger<RenderQueue> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        // Create bounded channel with specified capacity
        var options = new BoundedChannelOptions(_settings.MaxQueueLength)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _queue = Channel.CreateBounded<QueuedRenderJob>(options);
    }

    public ChannelReader<QueuedRenderJob> Reader => _queue.Reader;

    public async Task<RawBitmap> QueueRenderAsync(Lyapunov.Settings settings, CancellationToken cancellationToken = default)
    {
        var completionSource = new TaskCompletionSource<RawBitmap>();
        var job = new QueuedRenderJob
        {
            Settings = settings,
            CompletionSource = completionSource,
            QueuedAt = DateTime.UtcNow
        };

        try
        {
            // Try to write to queue with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_settings.MaxWaitTime);

            if (!await _queue.Writer.WaitToWriteAsync(timeoutCts.Token))
            {
                throw new InvalidOperationException("Render queue is closed");
            }

            if (!_queue.Writer.TryWrite(job))
            {
                throw new InvalidOperationException("Failed to queue render job");
            }

            _logger.LogInformation("Queued render job: {Width}x{Height}, Pattern: {Pattern}",
                settings.Size.Width, settings.Size.Height, settings.Pattern);

            // Wait for completion with timeout
            using var completionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            completionCts.CancelAfter(_settings.MaxWaitTime);

            return await completionSource.Task.WaitAsync(completionCts.Token);
        }
        catch (OperationCanceledException)
        {
            completionSource.TrySetCanceled(cancellationToken);
            throw new TimeoutException("Render request timed out");
        }
        catch (Exception ex)
        {
            completionSource.TrySetException(ex);
            _logger.LogError(ex, "Failed to queue render job");
            throw;
        }
    }

    public QueueStatus GetQueueStatus()
    {
        // Note: Getting exact queue length from Channel is not straightforward
        // This is an approximation for status reporting
        var queueLength = _queue.Reader.TryPeek(out _) ? 1 : 0;

        return new QueueStatus
        {
            QueueLength = queueLength,
            ActiveJobs = _activeJobs,
            MaxParallelJobs = _settings.MaxParallelJobs,
            MaxQueueLength = _settings.MaxQueueLength,
            Timestamp = DateTime.UtcNow
        };
    }

    public void IncrementActiveJobs() => Interlocked.Increment(ref _activeJobs);
    public void DecrementActiveJobs() => Interlocked.Decrement(ref _activeJobs);
}