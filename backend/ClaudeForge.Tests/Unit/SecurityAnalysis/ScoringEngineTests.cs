using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Core.Modules.SecurityAnalysis.Services;

namespace ClaudeForge.Tests.Unit.SecurityAnalysis;

/// <summary>
/// Unit tests for ScoringEngine — pure domain logic for weighted score calculation.
/// Default weights: static 0.6, dynamic 0.4.
///
/// Uses real ScoringEngine, not mocks — pure functions.
/// </summary>
public sealed class ScoringEngineTests
{
    private static readonly ScoringEngine Engine = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ScoringConfig DefaultConfig() => new()
    {
        StaticWeight = 0.6m,
        DynamicWeight = 0.4m,
        PassThreshold = 80m,
        FailThreshold = 50m,
    };

    private static CombinedStaticResult MakeStatic(decimal eslint, decimal semgrep, decimal gitleaks, decimal trivy)
    {
        decimal avg = (eslint + semgrep + gitleaks + trivy) / 4m;
        return new CombinedStaticResult(eslint, semgrep, gitleaks, trivy, avg, []);
    }

    private static DynamicAnalysisResult MakeDynamic(decimal behaviorScore) =>
        new(behaviorScore, [], null, null);

    // ── T.1: Default weights produce correct weighted score ──────────────────

    [Fact]
    public void CalculateScore_DefaultWeights_CorrectWeightedScore()
    {
        // Arrange: static avg = 70, dynamic = 90
        CombinedStaticResult? staticResult = MakeStatic(70, 70, 70, 70);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(90);
        ScoringConfig config = DefaultConfig();

        // Act
        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, config);

        // Assert: total = (70 * 0.6) + (90 * 0.4) = 42 + 36 = 78
        Assert.Equal(70m, result.StaticScore);
        Assert.Equal(90m, result.DynamicScore);
        Assert.Equal(78m, result.TotalScore);
        Assert.Equal(0.6m, result.StaticWeight);
        Assert.Equal(0.4m, result.DynamicWeight);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void CalculateScore_StaticNull_UsesDynamicOnly()
    {
        // static null → default 100, then weighted: (100 * 0.6) + (80 * 0.4) = 60 + 32 = 92
        DynamicAnalysisResult? dynamicResult = MakeDynamic(80);
        ScoreResult result = Engine.CalculateScore(null, dynamicResult, DefaultConfig());

        Assert.Equal(100m, result.StaticScore);
        Assert.Equal(80m, result.DynamicScore);
        Assert.Equal(92m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_DynamicNull_UsesStaticOnly()
    {
        // dynamic null → default 100, then weighted: (60 * 0.6) + (100 * 0.4) = 36 + 40 = 76
        CombinedStaticResult? staticResult = MakeStatic(60, 60, 60, 60);
        ScoreResult result = Engine.CalculateScore(staticResult, null, DefaultConfig());

        Assert.Equal(60m, result.StaticScore);
        Assert.Equal(100m, result.DynamicScore);
        Assert.Equal(76m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_BothNull_DefaultsTo100()
    {
        // Both null → both default to 100: (100 * 0.6) + (100 * 0.4) = 60 + 40 = 100
        ScoreResult result = Engine.CalculateScore(null, null, DefaultConfig());

        Assert.Equal(100m, result.StaticScore);
        Assert.Equal(100m, result.DynamicScore);
        Assert.Equal(100m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_BothScoresZero_TotalIsZero()
    {
        CombinedStaticResult? staticResult = MakeStatic(0, 0, 0, 0);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(0);
        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, DefaultConfig());

        Assert.Equal(0m, result.StaticScore);
        Assert.Equal(0m, result.DynamicScore);
        Assert.Equal(0m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_BothScoresMax_TotalIsOneHundred()
    {
        CombinedStaticResult? staticResult = MakeStatic(100, 100, 100, 100);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(100);
        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, DefaultConfig());

        Assert.Equal(100m, result.StaticScore);
        Assert.Equal(100m, result.DynamicScore);
        Assert.Equal(100m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_DynamicScoreClampedTo100()
    {
        // Dynamic score > 100 clamped to 100
        CombinedStaticResult? staticResult = MakeStatic(80, 80, 80, 80);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(150);
        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, DefaultConfig());

        // total = (80 * 0.6) + (100 * 0.4) = 48 + 40 = 88
        Assert.Equal(80m, result.StaticScore);
        Assert.Equal(100m, result.DynamicScore);
        Assert.Equal(88m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_DynamicScoreClampedToZero()
    {
        // Dynamic score < 0 clamped to 0
        CombinedStaticResult? staticResult = MakeStatic(80, 80, 80, 80);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(-50);
        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, DefaultConfig());

        // total = (80 * 0.6) + (0 * 0.4) = 48 + 0 = 48
        Assert.Equal(0m, result.DynamicScore);
        Assert.Equal(48m, result.TotalScore);
    }

    // ── Custom weights ───────────────────────────────────────────────────────

    [Fact]
    public void CalculateScore_CustomWeights_UsesProvidedWeights()
    {
        CombinedStaticResult? staticResult = MakeStatic(80, 80, 80, 80);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(60);
        ScoringConfig config = new()
        {
            StaticWeight = 0.3m,
            DynamicWeight = 0.7m,
            PassThreshold = 80m,
            FailThreshold = 50m,
        };

        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, config);

        // total = (80 * 0.3) + (60 * 0.7) = 24 + 42 = 66
        Assert.Equal(66m, result.TotalScore);
        Assert.Equal(0.3m, result.StaticWeight);
        Assert.Equal(0.7m, result.DynamicWeight);
    }

    // ── Static score averaging ───────────────────────────────────────────────

    [Fact]
    public void CalculateScore_PartialToolScores_AveragesAvailableOnly()
    {
        // Only eslint and semgrep have scores; gitleaks/trivy are -1 (didn't run)
        CombinedStaticResult? staticResult = new(
            EslintScore: 90m,
            SemgrepScore: 70m,
            GitleaksScore: -1m,
            TrivyScore: -1m,
            AverageScore: 0m, // ignored; engine recomputes
            AllFindings: []);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(100);

        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, DefaultConfig());

        // static avg = (90 + 70) / 2 = 80
        // total = (80 * 0.6) + (100 * 0.4) = 48 + 40 = 88
        Assert.Equal(80m, result.StaticScore);
        Assert.Equal(88m, result.TotalScore);
    }

    [Fact]
    public void CalculateScore_AllToolsNegative_ToolsDidNotRunDefaultsTo100()
    {
        CombinedStaticResult? staticResult = new(-1, -1, -1, -1, 0, []);
        DynamicAnalysisResult? dynamicResult = MakeDynamic(80);

        ScoreResult result = Engine.CalculateScore(staticResult, dynamicResult, DefaultConfig());

        // All negative → no tool data → default 100
        // total = (100 * 0.6) + (80 * 0.4) = 60 + 32 = 92
        Assert.Equal(100m, result.StaticScore);
        Assert.Equal(92m, result.TotalScore);
    }

    // ── Weight validation (delegated to ScoringConfigValidator) ──────────────

    [Fact]
    public void CalculateScore_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Engine.CalculateScore(null, null, null!));
    }

    [Fact]
    public void CalculateScore_WeightsNotSumTo1_ThrowsArgumentException()
    {
        ScoringConfig badConfig = new()
        {
            StaticWeight = 0.8m,
            DynamicWeight = 0.1m, // sum = 0.9, not 1.0
        };

        Assert.Throws<ArgumentException>(() =>
            Engine.CalculateScore(null, null, badConfig));
    }
}
