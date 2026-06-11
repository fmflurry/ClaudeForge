namespace ClaudeForge.Application.Modules.AddOnDistribution.Ports;

/// <summary>
/// Outgoing port for resolving plugin package metadata and tracking download counts.
/// </summary>
public interface IAddOnDistributionRepositoryPort
{
    /// <summary>
    /// Resolves the download metadata for the given plugin and optional version.
    /// When <paramref name="version"/> is <c>null</c>, the latest version (<c>is_latest=true</c>) is returned.
    /// </summary>
    Task<DownloadResolutionResult> ResolveAsync(
        Guid pluginId,
        string? version,
        CancellationToken ct = default);

    /// <summary>
    /// Atomically increments the download counter in all three tables:
    /// <c>telemetry_aggregates</c>, <c>plugin_versions</c>, and <c>plugins</c>.
    /// Must only be called on a confirmed successful download.
    /// </summary>
    Task IncrementDownloadCountAsync(
        Guid pluginId,
        string version,
        CancellationToken ct = default);
}
