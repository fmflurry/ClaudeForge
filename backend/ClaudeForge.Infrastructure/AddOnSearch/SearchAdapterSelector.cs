using ClaudeForge.Application.Modules.AddOnSearch.Ports;
using ClaudeForge.Application.Modules.AddOnSearch.UseCases;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Infrastructure.AddOnSearch;

/// <summary>
/// Routes ISearchIndexPort calls to either PostgresSearchAdapter or QdrantSearchAdapter
/// based on the Features:QdrantEnabled configuration flag.
/// </summary>
public sealed class SearchAdapterSelector : ISearchIndexPort
{
    private readonly ISearchIndexPort _selectedAdapter;

    public SearchAdapterSelector(
        ISearchIndexPort postgresAdapter,
        ISearchIndexPort qdrantAdapter,
        bool qdrantEnabled)
    {
        _selectedAdapter = qdrantEnabled ? qdrantAdapter : postgresAdapter;
    }

    public Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        return _selectedAdapter.SearchAsync(criteria, pagination, viewerOrgIds, ct);
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        CancellationToken ct = default)
        => _selectedAdapter.SearchAsync(criteria, pagination, ct);

    public Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        return _selectedAdapter.DiscoverAsync(criteria, viewerOrgIds, ct);
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        CancellationToken ct = default)
        => _selectedAdapter.DiscoverAsync(criteria, ct);
}
