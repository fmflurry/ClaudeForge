namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>
/// Combined result from all static analysis tools.
/// Each score is normalized to 0-100 (100 = no issues found).
/// AverageScore is the mean of the 4 tool scores.
/// </summary>
public sealed record CombinedStaticResult(
    decimal EslintScore,
    decimal SemgrepScore,
    decimal GitleaksScore,
    decimal TrivyScore,
    decimal AverageScore,
    IReadOnlyList<StaticFinding> AllFindings
);
