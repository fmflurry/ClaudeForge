using ClaudeForge.Core.Modules.SecurityAnalysis.Services;

namespace ClaudeForge.Tests.Unit.SecurityAnalysis;

/// <summary>
/// Unit tests for DecisionEngine — pure domain logic for plugin acceptance decisions.
///
/// Rules:
///   totalScore >= passThreshold → Pass ("passed")
///   failThreshold <= totalScore < passThreshold → Review ("in_review")
///   totalScore < failThreshold → Fail ("failed")
/// </summary>
public sealed class DecisionEngineTests
{
    private static readonly DecisionEngine Engine = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const decimal DefaultPass = 80m;
    private const decimal DefaultFail = 50m;

    // ── T.2: Score >= pass threshold → "passed" ──────────────────────────────

    [Fact]
    public void Decide_ScoreAtPassThreshold_ReturnsPassed()
    {
        DecisionResult result = Engine.Decide(80m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Pass, result.Decision);
        Assert.Equal("passed", result.Status);
        Assert.Contains("passed", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_ScoreAbovePassThreshold_ReturnsPassed()
    {
        DecisionResult result = Engine.Decide(95m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Pass, result.Decision);
        Assert.Equal("passed", result.Status);
    }

    [Fact]
    public void Decide_CustomPassThreshold_Respected()
    {
        // Custom pass threshold 70 — score 75 should pass
        DecisionResult result = Engine.Decide(75m, passThreshold: 70m, failThreshold: 40m);

        Assert.Equal(Decision.Pass, result.Decision);
        Assert.Equal("passed", result.Status);
    }

    // ── Score < fail threshold → "failed" ────────────────────────────────────

    [Fact]
    public void Decide_ScoreAtFailThreshold_ReturnsInReview()
    {
        // At fail threshold (50) but below pass (80) → in_review
        DecisionResult result = Engine.Decide(50m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Review, result.Decision);
        Assert.Equal("in_review", result.Status);
    }

    [Fact]
    public void Decide_ScoreBelowFailThreshold_ReturnsFailed()
    {
        DecisionResult result = Engine.Decide(30m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Fail, result.Decision);
        Assert.Equal("failed", result.Status);
        Assert.Contains("rejected", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_ScoreZero_ReturnsFailed()
    {
        DecisionResult result = Engine.Decide(0m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Fail, result.Decision);
        Assert.Equal("failed", result.Status);
    }

    // ── Score between thresholds → "in_review" ───────────────────────────────

    [Fact]
    public void Decide_ScoreBetweenThresholds_ReturnsInReview()
    {
        // 65 is between 50 (fail) and 80 (pass)
        DecisionResult result = Engine.Decide(65m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Review, result.Decision);
        Assert.Equal("in_review", result.Status);
        Assert.Contains("manual review", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_ScoreJustAboveFailThreshold_ReturnsInReview()
    {
        DecisionResult result = Engine.Decide(51m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Review, result.Decision);
        Assert.Equal("in_review", result.Status);
    }

    [Fact]
    public void Decide_ScoreJustBelowPassThreshold_ReturnsInReview()
    {
        DecisionResult result = Engine.Decide(79m, DefaultPass, DefaultFail);

        Assert.Equal(Decision.Review, result.Decision);
        Assert.Equal("in_review", result.Status);
    }

    // ── Custom config overrides ──────────────────────────────────────────────

    [Fact]
    public void Decide_StrictThresholds_Score85PassesWithPass90()
    {
        // Strict org: pass=90, fail=70 → 85 is between → review
        DecisionResult result = Engine.Decide(85m, passThreshold: 90m, failThreshold: 70m);

        Assert.Equal(Decision.Review, result.Decision);
        Assert.Equal("in_review", result.Status);
    }

    [Fact]
    public void Decide_LenientThresholds_Score60WithPass50Fail20_Passes()
    {
        DecisionResult result = Engine.Decide(60m, passThreshold: 50m, failThreshold: 20m);

        Assert.Equal(Decision.Pass, result.Decision);
        Assert.Equal("passed", result.Status);
    }

    [Fact]
    public void Decide_ScoreMinValue_AllBelowFailThreshold_ReturnsFailed()
    {
        DecisionResult result = Engine.Decide(10m, passThreshold: 80m, failThreshold: 50m);

        Assert.Equal(Decision.Fail, result.Decision);
        Assert.Equal("failed", result.Status);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    [Fact]
    public void Decide_FailThresholdGreaterThanPassThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Engine.Decide(60m, passThreshold: 50m, failThreshold: 70m));
    }

    [Fact]
    public void Decide_NegativeTotalScore_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Engine.Decide(-1m, DefaultPass, DefaultFail));
    }

    [Fact]
    public void Decide_ScoreAbove100_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Engine.Decide(101m, DefaultPass, DefaultFail));
    }
}
