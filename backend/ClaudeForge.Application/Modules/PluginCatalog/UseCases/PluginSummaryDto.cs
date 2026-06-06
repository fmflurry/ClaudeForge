namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// DTO for a plugin summary item in a paginated list.
/// </summary>
public sealed record PluginSummaryDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
    public required long DownloadCount { get; init; }
    public required string? LatestVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required IReadOnlyList<string> Types { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<string> UseCases { get; init; }
}
