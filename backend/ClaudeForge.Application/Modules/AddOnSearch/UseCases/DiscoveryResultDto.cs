namespace ClaudeForge.Application.Modules.AddOnSearch.UseCases;

/// <summary>
/// Represents a single plugin discovery result with full contextual metadata.
/// MaturityIndicator is one of: "new", "stable", "deprecated".
/// </summary>
public sealed record DiscoveryResultDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? LatestVersion { get; init; }
    public required IReadOnlyList<string> Types { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<string> UseCases { get; init; }
    public required float RelevanceScore { get; init; }
    public required long DownloadCount { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
    public required string Author { get; init; }
    public required string MaturityIndicator { get; init; }
}
