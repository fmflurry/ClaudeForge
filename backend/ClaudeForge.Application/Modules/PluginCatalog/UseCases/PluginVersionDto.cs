namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// DTO for a single plugin version entry in the version history.
/// </summary>
public sealed record PluginVersionDto
{
    public required string VersionNumber { get; init; }
    public required DateTimeOffset ReleaseDate { get; init; }
    public required string ReleaseNotes { get; init; }
    public required long DownloadCount { get; init; }
    public required bool IsLatest { get; init; }
}
