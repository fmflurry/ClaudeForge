namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// DTO for full plugin details including version history.
/// Extends the summary fields with the full versions list.
/// </summary>
public sealed record PluginDetailDto
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
    public required IReadOnlyList<string> UseCaseTags { get; init; }
    public required IReadOnlyList<PluginVersionDto> Versions { get; init; }
}
