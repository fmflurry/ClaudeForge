namespace ClaudeForge.Application.Modules.AddOnPublishing.Ports;

/// <summary>
/// Command to add a new version to an existing plugin.
/// </summary>
public sealed record AddVersionCommand(
    string Version,
    long VersionSort,
    string PackageKey,
    string PackageFormat,
    long SizeBytes,
    string Sha256,
    string ReleaseNotes,
    string? ReadmeText = null);
