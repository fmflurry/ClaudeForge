namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Input query for searching plugins with optional full-text query and category filters.
/// </summary>
public sealed record SearchPluginsQuery
{
    public string? Q { get; init; }
    public IReadOnlyList<string>? TypeFilter { get; init; }
    public IReadOnlyList<string>? LanguageFilter { get; init; }
    public IReadOnlyList<string>? UseCaseFilter { get; init; }
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
}
