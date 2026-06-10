using ClaudeForge.Core.Modules.SecurityAnalysis.Services;

namespace ClaudeForge.Tests.Unit.SecurityAnalysis;

/// <summary>
/// Unit tests for ScoringConfigValidator.
/// Validates scoring configuration constraints.
/// </summary>
public sealed class ScoringConfigValidatorTests
{
    // ── Valid config ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_DefaultConfig_Passes()
    {
        ScoringConfig config = new();
        // Should not throw
        ScoringConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_CustomValidConfig_Passes()
    {
        ScoringConfig config = new()
        {
            StaticWeight = 0.3m,
            DynamicWeight = 0.7m,
            PassThreshold = 75m,
            FailThreshold = 40m,
        };

        ScoringConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_ZeroWeights_Passes()
    {
        // Edge: 100% dynamic, 0% static — still valid if sum is 1.0
        ScoringConfig config = new()
        {
            StaticWeight = 0m,
            DynamicWeight = 1.0m,
        };

        ScoringConfigValidator.Validate(config);
    }

    // ── Weights not summing to 1.0 ───────────────────────────────────────────

    [Fact]
    public void Validate_WeightsTooLow_Throws()
    {
        ScoringConfig config = new()
        {
            StaticWeight = 0.4m,
            DynamicWeight = 0.4m, // sum = 0.8
        };

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
        Assert.Contains("sum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WeightsTooHigh_Throws()
    {
        ScoringConfig config = new()
        {
            StaticWeight = 0.7m,
            DynamicWeight = 0.5m, // sum = 1.2
        };

        Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_WeightsAlmostSumTo1_PassesWithinTolerance()
    {
        // Tolerance is ±0.01
        ScoringConfig config = new()
        {
            StaticWeight = 0.61m,
            DynamicWeight = 0.40m, // sum = 1.01
        };

        // Should pass (within ±0.01 tolerance)
        ScoringConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_WeightsJustOutsideTolerance_Throws()
    {
        ScoringConfig config = new()
        {
            StaticWeight = 0.62m,
            DynamicWeight = 0.40m, // sum = 1.02, outside ±0.01
        };

        Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
    }

    // ── Pass threshold <= fail threshold ─────────────────────────────────────

    [Fact]
    public void Validate_PassThresholdEqualsFailThreshold_Throws()
    {
        ScoringConfig config = new()
        {
            PassThreshold = 60m,
            FailThreshold = 60m,
        };

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
        Assert.Contains("PassThreshold", ex.Message);
    }

    [Fact]
    public void Validate_PassThresholdLessThanFailThreshold_Throws()
    {
        ScoringConfig config = new()
        {
            PassThreshold = 40m,
            FailThreshold = 60m,
        };

        Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
    }

    // ── Negative values rejected ─────────────────────────────────────────────

    [Fact]
    public void Validate_NegativeStaticWeight_Throws()
    {
        ScoringConfig config = new()
        {
            StaticWeight = -0.1m,
            DynamicWeight = 1.1m,
        };

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
        Assert.Contains("non-negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NegativeDynamicWeight_Throws()
    {
        ScoringConfig config = new()
        {
            StaticWeight = 1.1m,
            DynamicWeight = -0.1m,
        };

        Assert.Throws<ArgumentException>(() =>
            ScoringConfigValidator.Validate(config));
    }

    // ── Null config ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ScoringConfigValidator.Validate(null!));
    }
}
