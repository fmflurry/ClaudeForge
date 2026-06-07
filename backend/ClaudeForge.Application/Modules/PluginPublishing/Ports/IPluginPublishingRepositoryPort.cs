using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginPublishing.Ports;

/// <summary>
/// Outgoing port for plugin publishing persistence operations.
/// </summary>
public interface IPluginPublishingRepositoryPort
{
    /// <summary>
    /// Creates a new plugin record along with its initial version atomically.
    /// </summary>
    Task<PluginPublishResult> CreatePluginWithInitialVersionAsync(
        CreatePluginCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new version to an existing plugin. Atomically flips the prior is_latest to false
    /// and marks the new version as is_latest = true.
    /// </summary>
    Task<PluginVersionPublishResult> AddVersionAsync(
        Guid pluginId,
        AddVersionCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when a plugin with the given normalized name already exists.
    /// The nameNormalized parameter must already be lower-invariant.
    /// </summary>
    Task<bool> ExistsByNameNormalizedAsync(
        string nameNormalized,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when a plugin with the given ID exists.
    /// </summary>
    Task<bool> PluginExistsAsync(
        Guid pluginId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when the given (pluginId, version) pair already exists.
    /// </summary>
    Task<bool> VersionExistsAsync(
        Guid pluginId,
        string version,
        CancellationToken ct = default);

    /// <summary>
    /// Returns paginated version history for a plugin ordered by version_sort descending (semver desc).
    /// </summary>
    Task<(IReadOnlyList<VersionHistoryDto> Items, int TotalCount)> GetVersionHistoryAsync(
        Guid pluginId,
        PaginationRequest pagination,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a single version detail or null when not found.
    /// </summary>
    Task<VersionDetailDto?> GetVersionAsync(
        Guid pluginId,
        string version,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the visibility and optionally the ownerOrgId of a plugin.
    /// When changing to "public", ownerOrgId should be null.
    /// When changing to "private", ownerOrgId must be provided.
    /// Returns (currentVisibility, currentOwnerOrgId) before the update, or null if the plugin is not found.
    /// </summary>
    Task<(string Visibility, Guid? OwnerOrgId)?> GetPluginVisibilityAsync(
        Guid pluginId,
        CancellationToken ct = default);

    /// <summary>
    /// Persists the new visibility and ownerOrgId for the plugin.
    /// </summary>
    Task UpdateVisibilityAsync(
        Guid pluginId,
        string visibility,
        Guid? ownerOrgId,
        CancellationToken ct = default);
}
