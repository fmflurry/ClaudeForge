using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.AddOnSearch.UseCases;

/// <summary>
/// Result of the SearchAddOnsUseCase.
/// CategorySuggestions is populated only when Envelope.Data is empty.
/// </summary>
public sealed record SearchAddOnsResult
{
    public required PaginatedEnvelope<SearchResultDto> Envelope { get; init; }
    public required IReadOnlyList<string> CategorySuggestions { get; init; }
}
