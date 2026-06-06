namespace ClaudeForge.Core.Domain.Plugins;

/// <summary>
/// Immutable domain entity representing a specific release of a plugin.
/// Pure domain — zero EF Core or infrastructure references.
/// </summary>
public sealed record PluginVersion
{
    public required Guid Id { get; init; }
    public required Guid PluginId { get; init; }
    public required SemVer Version { get; init; }
    public long VersionSort { get; init; }
    public string ReleaseNotes { get; init; } = string.Empty;
    public bool IsLatest { get; init; }
    public required string PackageKey { get; init; }
    public required string PackageFormat { get; init; }
    public required long SizeBytes { get; init; }
    public required string Sha256 { get; init; }
    public long DownloadCount { get; init; }
    public string? ReadmeText { get; init; }
    public required DateTimeOffset ReleasedAt { get; init; }
}
