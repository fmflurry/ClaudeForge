namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>analysis_results</c> table.
/// Stores static and dynamic analysis scores and findings for a plugin version.
/// </summary>
public sealed class AnalysisResultEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → plugins ON DELETE CASCADE.</summary>
    public Guid PluginId { get; set; }

    /// <summary>Semantic version string of the analyzed plugin version.</summary>
    public string PluginVersion { get; set; } = string.Empty;

    // Static analysis scores (nullable until the analysis tool runs)
    public decimal? StaticEslintScore { get; set; }
    public decimal? StaticSemgrepScore { get; set; }
    public decimal? StaticGitleaksScore { get; set; }
    public decimal? StaticTrivyScore { get; set; }

    /// <summary>JSONB array of static analysis findings: [{severity, message, file, line}].</summary>
    public string StaticFindings { get; set; } = "[]";

    // Dynamic analysis
    public decimal? DynamicBehaviorScore { get; set; }

    /// <summary>JSONB array of dynamic analysis findings: [{type, description, severity}].</summary>
    public string DynamicFindings { get; set; } = "[]";

    // Overall
    public decimal TotalScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? AnalysisCompletedAt { get; set; }

    // Thresholds
    public decimal StaticWeight { get; set; } = 0.6m;
    public decimal DynamicWeight { get; set; } = 0.4m;
    public decimal PassThreshold { get; set; } = 80m;
    public decimal FailThreshold { get; set; } = 50m;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public PluginEntity Plugin { get; set; } = null!;
}
