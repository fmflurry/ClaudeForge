using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using Microsoft.Extensions.Logging;

namespace ClaudeForge.Infrastructure.PluginSearch;

/// <summary>
/// Qdrant vector search adapter seam (MVP stub).
/// In the current MVP, Qdrant is not implemented.
/// All calls are delegated to the FTS fallback (PostgresSearchAdapter) and the fallback event is logged.
/// This seam satisfies the adapter selector test expectations and is ready for full Qdrant implementation.
/// </summary>
public sealed class QdrantSearchAdapter : ISearchIndexPort
{
    private readonly ISearchIndexPort _ftsFallback;
    private readonly ILogger<QdrantSearchAdapter> _logger;

    public QdrantSearchAdapter(ISearchIndexPort ftsFallback, ILogger<QdrantSearchAdapter> logger)
    {
        _ftsFallback = ftsFallback;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Qdrant search not available in MVP — falling back to FTS for query: {Query}",
            criteria.Query);

        return await _ftsFallback.SearchAsync(criteria, pagination, viewerOrgIds, ct);
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Qdrant search not available in MVP — falling back to FTS for query: {Query}",
            criteria.Query);
        return _ftsFallback.SearchAsync(criteria, pagination, ct);
    }

    public async Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Qdrant discovery not available in MVP — falling back to FTS for query: {Query}",
            criteria.Query);

        return await _ftsFallback.DiscoverAsync(criteria, viewerOrgIds, ct);
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Qdrant discovery not available in MVP — falling back to FTS for query: {Query}",
            criteria.Query);
        return _ftsFallback.DiscoverAsync(criteria, ct);
    }
}
