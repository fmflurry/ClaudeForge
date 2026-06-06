using ClaudeForge.Application.Modules.PluginDistribution.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Core.Ports;

namespace ClaudeForge.Application.Modules.PluginDistribution.UseCases;

/// <summary>
/// Resolves a plugin package, streams it from storage, and increments the download counter on success.
/// </summary>
public sealed class DownloadPluginUseCase
{
    private readonly IPluginDistributionRepositoryPort _repo;
    private readonly IPackageStoragePort _storage;

    public DownloadPluginUseCase(
        IPluginDistributionRepositoryPort repo,
        IPackageStoragePort storage)
    {
        _repo = repo;
        _storage = storage;
    }

    /// <summary>
    /// Resolves and returns the download artifact for the given plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="version">
    /// The explicit semver version string, <c>"latest"</c>, or <c>null</c> to request the latest version.
    /// Any value other than null/<c>"latest"</c> must be a valid semver string; otherwise
    /// <see cref="InvalidDownloadVersionFormatException"/> is thrown.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="DownloadResult"/> containing the stream and response metadata.</returns>
    public async Task<DownloadResult> ExecuteAsync(
        Guid pluginId,
        string? version,
        CancellationToken ct = default)
    {
        // Determine the resolved version string to pass to the repository.
        // null and "latest" both mean "give me the is_latest row".
        string? resolvedQuery = NormalizeVersion(version);

        DownloadResolutionResult result = await _repo.ResolveAsync(pluginId, resolvedQuery, ct);

        return result switch
        {
            PluginNotFoundResult =>
                throw new PluginNotFoundException(),

            VersionNotFoundResult vnf =>
                throw new VersionNotFoundException(vnf.Version),

            FoundResult found =>
                await StreamAndIncrementAsync(pluginId, found.Resolution, ct),

            _ => throw new InvalidOperationException(
                $"Unhandled {nameof(DownloadResolutionResult)} variant: {result.GetType().Name}"),
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates the version string (if not null/"latest"), then normalises to the value the
    /// repository should receive (<c>null</c> for latest, the raw string for explicit versions).
    /// </summary>
    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrEmpty(version) || version == "latest")
            return null;

        try
        {
            SemVer.Parse(version);
        }
        catch (ArgumentException)
        {
            throw new InvalidDownloadVersionFormatException();
        }

        return version;
    }

    /// <summary>
    /// Fetches the package stream, increments the download counter, and returns the result.
    /// Counter is incremented after the stream is obtained so the increment only happens
    /// when the package is ready to be served.
    /// </summary>
    private async Task<DownloadResult> StreamAndIncrementAsync(
        Guid pluginId,
        DownloadResolution resolution,
        CancellationToken ct)
    {
        Stream stream = await _storage.GetAsync(resolution.PackageKey, ct);

        await _repo.IncrementDownloadCountAsync(pluginId, resolution.Version, ct);

        string fileName = BuildFileName(resolution.PluginName, resolution.Version, resolution.PackageFormat);
        string contentType = resolution.PackageFormat == "tar.gz"
            ? "application/gzip"
            : "application/zip";

        return new DownloadResult(
            Stream: stream,
            FileName: fileName,
            ContentType: contentType,
            SizeBytes: resolution.SizeBytes,
            Sha256: resolution.Sha256);
    }

    private static string BuildFileName(string pluginName, string version, string packageFormat)
    {
        string extension = packageFormat == "tar.gz" ? "tar.gz" : "zip";
        return $"{pluginName}-{version}.{extension}";
    }
}
