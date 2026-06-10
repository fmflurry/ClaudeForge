namespace ClaudeForge.Core.Modules.SecurityAnalysis.Services;

/// <summary>
/// Configuration for the scoring engine.
/// Weights determine how static and dynamic scores contribute to the total.
/// Default weights: static 0.6, dynamic 0.4.
/// Stored per analysis result in <c>AnalysisResultEntity.StaticWeight</c> and <c>DynamicWeight</c>.
/// </summary>
public sealed record ScoringConfig
{
    public decimal StaticWeight { get; init; } = 0.6m;
    public decimal DynamicWeight { get; init; } = 0.4m;
    public decimal PassThreshold { get; init; } = 80m;
    public decimal FailThreshold { get; init; } = 50m;
}
