namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Represents a single plugin search result with relevance and metadata.
/// </summary>
public sealed record SearchResultDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Description { get; init; }
    public required float RelevanceScore { get; init; }
    public required long DownloadCount { get; init; }
    public string? LatestVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<string> Types { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<string> UseCases { get; init; }
}
