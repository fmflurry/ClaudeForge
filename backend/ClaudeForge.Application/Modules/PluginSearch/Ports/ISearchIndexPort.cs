using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginSearch.Ports;

/// <summary>
/// Port for plugin search and discovery operations.
/// Implementations may use full-text search (Postgres tsvector) or vector search (Qdrant).
/// </summary>
public interface ISearchIndexPort
{
    /// <summary>
    /// Searches plugins matching the given criteria, filtered to only include plugins
    /// visible to the caller: visibility='public' OR owner_org_id = ANY(viewerOrgIds).
    /// </summary>
    Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default);

    /// <summary>
    /// Backward-compatible overload without viewerOrgIds (public-only).
    /// </summary>
    Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        return SearchAsync(criteria, pagination, new HashSet<Guid>(), ct);
    }

    /// <summary>
    /// Discovers plugins matching the given criteria, filtered to only include plugins
    /// visible to the caller: visibility='public' OR owner_org_id = ANY(viewerOrgIds).
    /// </summary>
    Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default);

    /// <summary>
    /// Backward-compatible overload without viewerOrgIds (public-only).
    /// </summary>
    Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        CancellationToken ct = default)
    {
        return DiscoverAsync(criteria, new HashSet<Guid>(), ct);
    }
}
