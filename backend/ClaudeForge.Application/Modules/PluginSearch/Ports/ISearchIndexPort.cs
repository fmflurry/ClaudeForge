using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginSearch.Ports;

/// <summary>
/// Port for plugin search and discovery operations.
/// Implementations may use full-text search (Postgres tsvector) or vector search (Qdrant).
/// </summary>
public interface ISearchIndexPort
{
    Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        CancellationToken ct = default);

    Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        CancellationToken ct = default);
}
