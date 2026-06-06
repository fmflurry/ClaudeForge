using ClaudeForge.Application.Modules.Docs.Ports;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.Docs.UseCases;

/// <summary>
/// Searches documentation pages by full-text query.
/// Clamps Limit to maximum 20 per spec.
/// Empty/null query returns empty envelope gracefully (no exception).
/// Results order is preserved from repository (ranked by relevance).
/// </summary>
public sealed class SearchDocsUseCase
{
    private const int MaxLimit = 20;

    private readonly IDocsRepositoryPort _repo;

    public SearchDocsUseCase(IDocsRepositoryPort repo)
    {
        _repo = repo;
    }

    public async Task<PaginatedEnvelope<DocSearchResultDto>> ExecuteAsync(
        SearchDocsQuery query,
        CancellationToken ct = default)
    {
        int clampedLimit = Math.Min(query.Limit, MaxLimit);

        PaginationRequest pagination = new()
        {
            Page = query.Page,
            Limit = clampedLimit,
        };

        string searchTerm = query.Search?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new PaginatedEnvelope<DocSearchResultDto>
            {
                Data = [],
                TotalCount = 0,
                Page = query.Page,
                Limit = clampedLimit,
            };
        }

        (IReadOnlyList<DocSearchResultDto> items, int totalCount) =
            await _repo.SearchAsync(searchTerm, pagination, ct);

        return new PaginatedEnvelope<DocSearchResultDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = query.Page,
            Limit = clampedLimit,
        };
    }
}
