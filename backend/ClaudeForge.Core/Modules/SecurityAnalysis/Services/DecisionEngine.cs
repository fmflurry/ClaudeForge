namespace ClaudeForge.Core.Modules.SecurityAnalysis.Services;

/// <summary>
/// Pure domain logic for deciding plugin acceptance based on total score and thresholds.
/// Rules:
///   totalScore >= passThreshold → Pass (status: "passed")
///   totalScore >= failThreshold && totalScore < passThreshold → Review (status: "in_review")
///   totalScore < failThreshold → Fail (status: "failed")
/// </summary>
public sealed class DecisionEngine
{
    /// <summary>
    /// Applies thresholds and returns a decision.
    /// </summary>
    /// <param name="totalScore">Combined security score (0-100).</param>
    /// <param name="passThreshold">Minimum score for automatic pass (default 80).</param>
    /// <param name="failThreshold">Score below which plugin is rejected (default 50).</param>
    public DecisionResult Decide(decimal totalScore, decimal passThreshold, decimal failThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(failThreshold, passThreshold, nameof(failThreshold));
        ArgumentOutOfRangeException.ThrowIfNegative(totalScore);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(totalScore, 100m, nameof(totalScore));

        if (totalScore >= passThreshold)
        {
            return new DecisionResult(
                Decision.Pass,
                "passed",
                $"Plugin passed with score {totalScore:F1} (threshold: ≥{passThreshold}). " +
                "Automatically accepted to catalog.");
        }

        if (totalScore >= failThreshold)
        {
            return new DecisionResult(
                Decision.Review,
                "in_review",
                $"Plugin requires manual review (score: {totalScore:F1}, " +
                $"pass threshold: {passThreshold}, fail threshold: {failThreshold}).");
        }

        return new DecisionResult(
            Decision.Fail,
            "failed",
            $"Plugin rejected with score {totalScore:F1} (threshold: <{failThreshold}). " +
            "Detailed findings available. Author may appeal.");
    }
}
