namespace ClaudeForge.Application.Modules.AddOnSearch.UseCases;

/// <summary>
/// Input query for discovering plugins using a keyword and optional category filters.
/// </summary>
public sealed record DiscoverAddOnsQuery
{
    public string? Keyword { get; init; }
    public IReadOnlyList<string>? LanguageFilter { get; init; }
    public IReadOnlyList<string>? UseCaseFilter { get; init; }
    public IReadOnlyList<string>? TypeFilter { get; init; }
}
