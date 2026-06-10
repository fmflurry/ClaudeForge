namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>analysis_jobs</c> table.
/// PostgreSQL-based async queue for plugin security analysis.
/// Workers poll with SKIP LOCKED and update status as they process.
/// </summary>
public sealed class AnalysisJobEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → plugins ON DELETE CASCADE.</summary>
    public Guid PluginId { get; set; }

    /// <summary>Specific plugin version to analyze.</summary>
    public string PluginVersion { get; set; } = string.Empty;

    /// <summary>Job status: "queued" | "processing" | "completed" | "failed".</summary>
    public string Status { get; set; } = "queued";

    /// <summary>Higher priority jobs are picked up first.</summary>
    public int Priority { get; set; }

    /// <summary>Number of processing attempts so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Last error message if the job failed.</summary>
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation properties
    public PluginEntity Plugin { get; set; } = null!;
}
