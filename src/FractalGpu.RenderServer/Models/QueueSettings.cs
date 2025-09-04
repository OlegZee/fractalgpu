namespace FractalGpu.RenderServer.Models;

public class QueueSettings
{
    public int MaxParallelJobs { get; set; } = 2;
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxQueueLength { get; set; } = 10;
}

public class QueueStatus
{
    public int QueueLength { get; set; }
    public int ActiveJobs { get; set; }
    public int MaxParallelJobs { get; set; }
    public int MaxQueueLength { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}