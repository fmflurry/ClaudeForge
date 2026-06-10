using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.Security.Queue;

/// <summary>
/// PostgreSQL-based analysis job queue.
/// Uses SKIP LOCKED for safe concurrent dequeuing.
/// Orders by priority DESC, created_at ASC.
/// </summary>
public sealed class AnalysisQueue : IAnalysisQueue
{
    private readonly IDbContextFactory<MarketplaceDbContext> _contextFactory;
    private readonly ILogger<AnalysisQueue> _logger;

    public AnalysisQueue(
        IDbContextFactory<MarketplaceDbContext> contextFactory,
        ILogger<AnalysisQueue> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> EnqueueAsync(Guid pluginId, string version, int priority = 0, CancellationToken ct = default)
    {
        await using MarketplaceDbContext db = await _contextFactory.CreateDbContextAsync(ct);

        var entity = new AnalysisJobEntity
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            PluginVersion = version,
            Status = "queued",
            Priority = priority,
            Attempts = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.AnalysisJobs.Add(entity);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Enqueued analysis job {JobId} for plugin {PluginId} version {Version} (priority {Priority})",
            entity.Id, pluginId, version, priority);

        return entity.Id;
    }

    public async Task<AnalysisJobDto?> DequeueAsync(CancellationToken ct = default)
    {
        await using MarketplaceDbContext db = await _contextFactory.CreateDbContextAsync(ct);

        // SELECT ... FOR UPDATE SKIP LOCKED — PostgreSQL-specific
        // Dequeue the highest priority, oldest queued job
        AnalysisJobEntity? entity = await db.AnalysisJobs
            .FromSqlRaw(
                @"SELECT * FROM analysis_jobs
                  WHERE status = 'queued'
                  ORDER BY priority DESC, created_at ASC
                  LIMIT 1
                  FOR UPDATE SKIP LOCKED")
            .SingleOrDefaultAsync(ct);

        if (entity is null)
            return null;

        // Update to processing
        entity.Status = "processing";
        entity.Attempts++;
        entity.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Dequeued analysis job {JobId} for plugin {PluginId} (attempt {Attempt})",
            entity.Id, entity.PluginId, entity.Attempts);

        return new AnalysisJobDto(
            Id: entity.Id,
            PluginId: entity.PluginId,
            PluginVersion: entity.PluginVersion,
            Priority: entity.Priority,
            Attempts: entity.Attempts,
            LastError: entity.LastError);
    }

    public async Task MarkCompletedAsync(Guid jobId, CancellationToken ct = default)
    {
        await using MarketplaceDbContext db = await _contextFactory.CreateDbContextAsync(ct);

        AnalysisJobEntity? entity = await db.AnalysisJobs.FindAsync([jobId], ct);
        if (entity is null)
        {
            _logger.LogWarning("Attempted to mark unknown job {JobId} as completed.", jobId);
            return;
        }

        entity.Status = "completed";
        entity.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Analysis job {JobId} completed.", jobId);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, CancellationToken ct = default)
    {
        await using MarketplaceDbContext db = await _contextFactory.CreateDbContextAsync(ct);

        AnalysisJobEntity? entity = await db.AnalysisJobs.FindAsync([jobId], ct);
        if (entity is null)
        {
            _logger.LogWarning("Attempted to mark unknown job {JobId} as failed.", jobId);
            return;
        }

        entity.Status = "failed";
        entity.LastError = error;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogError("Analysis job {JobId} failed: {Error}", jobId, error);
    }

    public async Task<QueueMetrics> GetQueueMetricsAsync(CancellationToken ct = default)
    {
        await using MarketplaceDbContext db = await _contextFactory.CreateDbContextAsync(ct);

        int queued = await db.AnalysisJobs.CountAsync(j => j.Status == "queued", ct);
        int processing = await db.AnalysisJobs.CountAsync(j => j.Status == "processing", ct);
        int completed = await db.AnalysisJobs.CountAsync(j => j.Status == "completed", ct);
        int failed = await db.AnalysisJobs.CountAsync(j => j.Status == "failed", ct);

        return new QueueMetrics(
            Queued: queued,
            Processing: processing,
            Completed: completed,
            Failed: failed,
            Total: queued + processing + completed + failed);
    }
}
