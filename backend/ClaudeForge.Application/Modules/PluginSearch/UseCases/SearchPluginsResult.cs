using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Result of the SearchPluginsUseCase.
/// CategorySuggestions is populated only when Envelope.Data is empty.
/// </summary>
public sealed record SearchPluginsResult
{
    public required PaginatedEnvelope<SearchResultDto> Envelope { get; init; }
    public required IReadOnlyList<string> CategorySuggestions { get; init; }
}
