namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>
/// Port for individual static analysis tool adapters.
/// Each tool (ESLint, Semgrep, Gitleaks, Trivy) implements this interface.
/// </summary>
public interface IStaticAnalyzer
{
    /// <summary>Human-readable tool name for logging and metrics.</summary>
    string ToolName { get; }

    /// <summary>
    /// Runs the static analysis tool against the plugin code directory.
    /// Returns a normalized score (0-100, higher = better) and any findings.
    /// Gracefully handles tool-not-found or tool failures (returns score 100, empty findings).
    /// </summary>
    Task<StaticAnalysisResult> AnalyzeAsync(string pluginCodeDirectory, CancellationToken ct);
}

/// <summary>
/// Result from a single static analysis tool run.
/// Score is normalized to 0-100 (100 = no issues found).
/// </summary>
public sealed record StaticAnalysisResult(
    decimal Score,
    IReadOnlyList<StaticFinding> Findings
);

/// <summary>
/// A single finding from static analysis.
/// </summary>
public sealed record StaticFinding(
    string Severity,   // "critical", "high", "medium", "low"
    string Message,
    string File,
    int? Line
);
