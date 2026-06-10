namespace ClaudeForge.Core.Modules.SecurityAnalysis.Services;

/// <summary>
/// Result of the scoring calculation.
/// </summary>
public sealed record ScoreResult(
    decimal StaticScore,
    decimal DynamicScore,
    decimal TotalScore,
    decimal StaticWeight,
    decimal DynamicWeight
);
