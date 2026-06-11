namespace ClaudeForge.Application.Modules.AddOnPublishing.Ports;

/// <summary>
/// DTO for a single entry in the paginated version history list.
/// </summary>
public sealed record VersionHistoryDto(
    Guid Id,
    string Version,
    long VersionSort,
    bool IsLatest,
    DateTimeOffset ReleasedAt,
    string ReleaseNotes,
    long DownloadCount);
