namespace ClaudeForge.Core.Modules.SecurityAnalysis.Services;

/// <summary>
/// Result of the decision engine.
/// Maps total score to a decision: Pass, Review, or Fail.
/// </summary>
public sealed record DecisionResult(
    Decision Decision,
    string Status,       // "passed", "in_review", "failed"
    string Reason        // Human-readable explanation
);

/// <summary>
/// Enumeration of possible decisions.
/// </summary>
public enum Decision
{
    /// <summary>Plugin accepted automatically (score >= passThreshold).</summary>
    Pass,

    /// <summary>Plugin needs manual review (score between failThreshold and passThreshold).</summary>
    Review,

    /// <summary>Plugin rejected (score < failThreshold).</summary>
    Fail
}
