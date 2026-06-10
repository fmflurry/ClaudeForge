using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Saves the completed analysis result to the database and updates plugin security status.
/// Delegates persistence to the <see cref="ISaveAnalysisResultPort"/> adapter.
/// </summary>
public sealed class SaveAnalysisResultUseCase
{
    private readonly ISaveAnalysisResultPort _repository;

    public SaveAnalysisResultUseCase(ISaveAnalysisResultPort repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Persists the full analysis result and updates the plugin's security score and status.
    /// </summary>
    public async Task<Guid> ExecuteAsync(SaveAnalysisResultCommand command, CancellationToken ct = default)
    {
        return await _repository.SaveAsync(command, ct);
    }
}
