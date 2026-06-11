namespace ClaudeForge.Application.Modules.AddOnDistribution.Ports;

/// <summary>
/// Resolved package metadata returned when a plugin and version are found.
/// </summary>
public sealed record DownloadResolution(
    string PluginName,
    string Version,
    string PackageKey,
    string PackageFormat,
    long SizeBytes,
    string Sha256);
