namespace ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

/// <summary>
/// Port for persisting analysis results and updating plugin security status.
/// Implemented by an infrastructure adapter using EF Core.
/// </summary>
public interface ISaveAnalysisResultPort
{
    /// <summary>
    /// Saves the analysis result and updates plugin security score/status.
    /// Returns the generated analysis result ID.
    /// </summary>
    Task<Guid> SaveAsync(SaveAnalysisResultCommand command, CancellationToken ct = default);
}
