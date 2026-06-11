using ClaudeForge.Application.Modules.AddOnCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.AddOnCatalog.Ports;

/// <summary>
/// Port for plugin data access operations.
/// Implemented by the infrastructure adapter (<c>AddOnRepositoryAdapter</c>).
/// </summary>
public interface IAddOnRepositoryPort
{
    /// <summary>
    /// Returns a paginated, filtered, and sorted list of plugin summaries plus the total count.
    /// Only plugins visible to the caller are included:
    ///   visibility='public' OR owner_org_id = ANY(viewerOrgIds).
    /// Pass an empty set for anonymous callers (public only).
    /// </summary>
    Task<(IReadOnlyList<AddOnSummaryDto> Items, int TotalCount)> ListAddOnsAsync(
        PaginationRequest pagination,
        string sortKey,
        string sortOrder,
        IReadOnlyList<string>? typeFilter,
        IReadOnlyList<string>? languageFilter,
        IReadOnlyList<string>? useCaseFilter,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default);

    /// <summary>
    /// Backward-compatible overload for callers without viewerOrgIds context (public-only).
    /// </summary>
    Task<(IReadOnlyList<AddOnSummaryDto> Items, int TotalCount)> ListAddOnsAsync(
        PaginationRequest pagination,
        string sortKey,
        string sortOrder,
        IReadOnlyList<string>? typeFilter,
        IReadOnlyList<string>? languageFilter,
        IReadOnlyList<string>? useCaseFilter,
        CancellationToken ct = default)
    {
        return ListAddOnsAsync(
            pagination, sortKey, sortOrder,
            typeFilter, languageFilter, useCaseFilter,
            new HashSet<Guid>(), ct);
    }

    /// <summary>
    /// Returns full plugin details including version history, or <c>null</c> if not found
    /// or if the plugin is not visible to the caller (non-disclosure: private to non-member → null).
    /// </summary>
    Task<AddOnDetailDto?> GetAddOnByIdAsync(
        Guid pluginId,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default);

    /// <summary>
    /// Backward-compatible overload for callers without viewerOrgIds context (public-only).
    /// </summary>
    Task<AddOnDetailDto?> GetAddOnByIdAsync(Guid pluginId, CancellationToken ct = default)
    {
        return GetAddOnByIdAsync(pluginId, new HashSet<Guid>());
    }

    /// <summary>
    /// Returns <c>true</c> when a plugin with the given normalized name already exists.
    /// The check is case-insensitive: callers must pass <c>name.ToLowerInvariant()</c>.
    /// </summary>
    Task<bool> ExistsByNameNormalizedAsync(string nameNormalized, CancellationToken ct = default);

    /// <summary>
    /// Returns the currently featured plugin as a <see cref="FeaturedAddOnDto"/>,
    /// or <c>null</c> when no plugin is flagged as featured.
    /// </summary>
    Task<FeaturedAddOnDto?> GetFeaturedAddOnAsync(CancellationToken ct = default);
}
