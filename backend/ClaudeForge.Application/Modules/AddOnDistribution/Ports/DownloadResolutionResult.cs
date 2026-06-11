namespace ClaudeForge.Application.Modules.AddOnDistribution.Ports;

/// <summary>
/// Discriminated union result returned by <see cref="IAddOnDistributionRepositoryPort.ResolveAsync"/>.
/// </summary>
public abstract record DownloadResolutionResult;

/// <summary>
/// The requested plugin does not exist.
/// </summary>
public sealed record PluginNotFoundResult : DownloadResolutionResult;

/// <summary>
/// The plugin exists but the explicitly requested version does not.
/// </summary>
public sealed record VersionNotFoundResult(string Version) : DownloadResolutionResult;

/// <summary>
/// The plugin and version were found; contains all data needed to stream the package
/// and to make the authorization decision.
/// </summary>
public sealed record FoundResult(
    DownloadResolution Resolution,
    string Visibility = "public",
    Guid? OwnerOrgId = null) : DownloadResolutionResult;
