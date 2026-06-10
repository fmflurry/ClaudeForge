using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

namespace ClaudeForge.Core.Modules.SecurityAnalysis.Services;

/// <summary>
/// Pure domain logic for weighted score calculation.
/// Static score = average of available tool scores (ESLint, Semgrep, Gitleaks, Trivy).
/// Dynamic score = behavior score directly.
/// Total = (staticScore * staticWeight) + (dynamicScore * dynamicWeight).
/// </summary>
public sealed class ScoringEngine
{
    /// <summary>
    /// Calculates the combined security score from static and dynamic analysis results.
    /// </summary>
    /// <param name="staticResult">Combined static analysis result (may have null scores if tools didn't run).</param>
    /// <param name="dynamicResult">Dynamic analysis result (may have null behavior score).</param>
    /// <param name="config">Scoring configuration with weights. Validated before calculation.</param>
    /// <returns>Calculated score result with breakdown.</returns>
    public ScoreResult CalculateScore(
        CombinedStaticResult? staticResult,
        DynamicAnalysisResult? dynamicResult,
        ScoringConfig config)
    {
        ScoringConfigValidator.Validate(config);

        // Static score: average of available tool scores
        // If no tools ran, default to 100 (no findings = safe)
        decimal staticScore = CalculateStaticScore(staticResult);

        // Dynamic score: behavior score directly
        // If dynamic analysis didn't run, default to 100 (no behavior observed = safe)
        decimal dynamicScore = dynamicResult?.BehaviorScore ?? 100m;

        // Clamp scores to valid range
        staticScore = ClampScore(staticScore);
        dynamicScore = ClampScore(dynamicScore);

        // Weighted total
        decimal totalScore = (staticScore * config.StaticWeight) + (dynamicScore * config.DynamicWeight);
        totalScore = ClampScore(totalScore);

        return new ScoreResult(
            StaticScore: staticScore,
            DynamicScore: dynamicScore,
            TotalScore: totalScore,
            StaticWeight: config.StaticWeight,
            DynamicWeight: config.DynamicWeight);
    }

    private static decimal CalculateStaticScore(CombinedStaticResult? staticResult)
    {
        if (staticResult is null)
            return 100m;

        var scores = new List<decimal>(4);

        if (staticResult.EslintScore >= 0) scores.Add(staticResult.EslintScore);
        if (staticResult.SemgrepScore >= 0) scores.Add(staticResult.SemgrepScore);
        if (staticResult.GitleaksScore >= 0) scores.Add(staticResult.GitleaksScore);
        if (staticResult.TrivyScore >= 0) scores.Add(staticResult.TrivyScore);

        return scores.Count > 0
            ? scores.Average()
            : 100m;
    }

    private static decimal ClampScore(decimal score) => Math.Clamp(score, 0m, 100m);
}
