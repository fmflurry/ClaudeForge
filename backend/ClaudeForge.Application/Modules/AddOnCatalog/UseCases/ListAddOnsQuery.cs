namespace ClaudeForge.Application.Modules.AddOnCatalog.UseCases;

/// <summary>
/// Input query for listing plugins with pagination, sorting, and category filters.
/// </summary>
public sealed record ListAddOnsQuery
{
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
    public string SortKey { get; init; } = "createdAt";
    public string SortOrder { get; init; } = "desc";
    public IReadOnlyList<string>? TypeFilter { get; init; }
    public IReadOnlyList<string>? LanguageFilter { get; init; }
    public IReadOnlyList<string>? UseCaseFilter { get; init; }
}
