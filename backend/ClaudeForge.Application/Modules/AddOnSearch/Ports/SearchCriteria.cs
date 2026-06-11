namespace ClaudeForge.Application.Modules.AddOnSearch.Ports;

/// <summary>
/// Criteria passed to ISearchIndexPort for both search and discovery operations.
/// Filter semantics: OR within a dimension, AND across dimensions.
/// </summary>
public sealed record SearchCriteria
{
    public string? Query { get; init; }
    public IReadOnlyList<string>? TypeFilter { get; init; }
    public IReadOnlyList<string>? LanguageFilter { get; init; }
    public IReadOnlyList<string>? UseCaseFilter { get; init; }
}
