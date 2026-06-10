namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>analysis_config</c> table.
/// Single-row configuration for the security analysis pipeline.
/// </summary>
public sealed class AnalysisConfigEntity
{
    /// <summary>Singleton row ID, always 1.</summary>
    public int Id { get; set; }

    /// <summary>Weight of static analysis scores (0.0–1.0). Default 0.6.</summary>
    public decimal StaticWeight { get; set; } = 0.6m;

    /// <summary>Weight of dynamic analysis scores (0.0–1.0). Default 0.4.</summary>
    public decimal DynamicWeight { get; set; } = 0.4m;

    /// <summary>Minimum total score to pass analysis. Default 80.</summary>
    public decimal PassThreshold { get; set; } = 80m;

    /// <summary>Maximum total score before failing. Default 50.</summary>
    public decimal FailThreshold { get; set; } = 50m;

    /// <summary>Maximum concurrent analysis workers. Default 2.</summary>
    public int MaxWorkers { get; set; } = 2;

    /// <summary>Retry limit for failed analysis jobs. Default 3.</summary>
    public int RetryLimit { get; set; } = 3;

    /// <summary>Analysis timeout in seconds. Default 300.</summary>
    public int AnalysisTimeoutSeconds { get; set; } = 300;

    /// <summary>When this config was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>User ID who last updated the config.</summary>
    public Guid? UpdatedBy { get; set; }
}
