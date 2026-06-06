namespace ClaudeForge.Application.Modules.PluginPublishing.Ports;

/// <summary>
/// DTO for a single version detail response.
/// </summary>
public sealed record VersionDetailDto(
    Guid Id,
    Guid PluginId,
    string Version,
    bool IsLatest,
    DateTimeOffset ReleasedAt,
    string ReleaseNotes,
    long DownloadCount,
    long SizeBytes,
    string Sha256,
    string PackageFormat);
