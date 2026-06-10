namespace ClaudeForge.Application.Modules.PluginPublishing.Ports;

/// <summary>
/// Command to create a new plugin with its initial version.
/// </summary>
public sealed record CreatePluginCommand(
    string Name,
    string NameNormalized,
    string Slug,
    string Description,
    string Author,
    string Version,
    long VersionSort,
    string PackageKey,
    string PackageFormat,
    long SizeBytes,
    string Sha256,
    string ReleaseNotes,
    string? ReadmeText,
    string Visibility = "public",
    Guid? OwnerOrgId = null,
    Guid? OwnerUserId = null,
    IReadOnlyList<short>? ResolvedCategoryIds = null);
