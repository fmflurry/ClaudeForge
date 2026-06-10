namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>
/// Port for dynamic analysis sandbox adapters.
/// The Docker sandbox analyzer is the primary implementation.
/// </summary>
public interface IDynamicAnalyzer
{
    /// <summary>
    /// Runs the plugin inside a sandbox (Docker container) and observes behavior.
    /// Returns behavior score (0-100) and any suspicious findings.
    /// Gracefully handles sandbox unavailability (returns default safe result with note).
    /// </summary>
    Task<DynamicAnalysisResult> AnalyzeAsync(string pluginPackagePath, CancellationToken ct);
}

/// <summary>
/// Result from dynamic analysis in a sandbox environment.
/// BehaviorScore is 0-100 (100 = no suspicious behavior detected).
/// </summary>
public sealed record DynamicAnalysisResult(
    decimal BehaviorScore,
    IReadOnlyList<DynamicFinding> Findings,
    string? SandboxOutput,
    string? SandboxError
);

/// <summary>
/// A single observation from dynamic analysis.
/// </summary>
public sealed record DynamicFinding(
    string Type,        // "file_access", "network_attempt", "process_spawn", "error", "warning"
    string Description,
    string Severity     // "critical", "high", "medium", "low"
);
