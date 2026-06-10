namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>
/// Port for the PG-based async analysis job queue.
/// Workers poll via DequeueAsync (SKIP LOCKED pattern) and update job status.
/// </summary>
public interface IAnalysisQueue
{
    /// <summary>Enqueue a new analysis job with optional priority.</summary>
    Task<Guid> EnqueueAsync(Guid pluginId, string version, int priority = 0, CancellationToken ct = default);

    /// <summary>
    /// Dequeue the highest-priority pending job using SELECT ... FOR UPDATE SKIP LOCKED.
    /// Returns null when no jobs are available.
    /// </summary>
    Task<AnalysisJobDto?> DequeueAsync(CancellationToken ct = default);

    /// <summary>Mark a job as completed successfully.</summary>
    Task MarkCompletedAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Mark a job as failed with an error message.</summary>
    Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct = default);

    /// <summary>Get queue metrics for monitoring.</summary>
    Task<QueueMetrics> GetQueueMetricsAsync(CancellationToken ct = default);
}

/// <summary>
/// DTO for a dequeued analysis job (Core domain, no EF entity dependency).
/// </summary>
public sealed record AnalysisJobDto(
    Guid Id,
    Guid PluginId,
    string PluginVersion,
    int Priority,
    int Attempts,
    string? LastError
);

/// <summary>
/// Queue health metrics.
/// </summary>
public sealed record QueueMetrics(
    int Queued,
    int Processing,
    int Completed,
    int Failed,
    int Total
);
