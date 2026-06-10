namespace ClaudeForge.Core.Modules.SecurityAnalysis.Services;

/// <summary>
/// Validates that a <see cref="ScoringConfig"/> has weights that sum to 1.0 (±0.01 tolerance).
/// </summary>
public static class ScoringConfigValidator
{
    private const decimal Tolerance = 0.01m;

    /// <summary>
    /// Validates the config. Throws <see cref="ArgumentException"/> if weights do not sum to 1.0.
    /// </summary>
    public static void Validate(ScoringConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        decimal sum = config.StaticWeight + config.DynamicWeight;
        if (Math.Abs(sum - 1.0m) > Tolerance)
        {
            throw new ArgumentException(
                $"Scoring weights must sum to 1.0, but static={config.StaticWeight} + dynamic={config.DynamicWeight} = {sum}. " +
                $"Allowed tolerance: ±{Tolerance}");
        }

        if (config.StaticWeight < 0 || config.DynamicWeight < 0)
        {
            throw new ArgumentException("Scoring weights must be non-negative.");
        }

        if (config.PassThreshold <= config.FailThreshold)
        {
            throw new ArgumentException(
                $"PassThreshold ({config.PassThreshold}) must be greater than FailThreshold ({config.FailThreshold}).");
        }
    }
}
