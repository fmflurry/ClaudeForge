using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Use case for searching plugins by full-text query and category filters.
/// Validates pagination, delegates to ISearchIndexPort, wraps results in PaginatedEnvelope.
/// When no results are found, populates CategorySuggestions.
/// </summary>
public sealed class SearchPluginsUseCase
{
    private static readonly IReadOnlyList<string> DefaultCategorySuggestions =
    [
        "skill",
        "hook",
        "agent",
        "command",
        "typescript",
        "python",
    ];

    private readonly ISearchIndexPort _index;

    public SearchPluginsUseCase(ISearchIndexPort index)
    {
        _index = index;
    }

    public async Task<SearchPluginsResult> ExecuteAsync(
        SearchPluginsQuery query,
        CancellationToken ct = default)
    {
        // Validate that page and limit are positive (spec: "Page and limit must be greater than 0").
        if (query.Page <= 0 || query.Limit <= 0)
        {
            throw new InvalidPaginationException();
        }

        // Clamp limit to the allowed maximum to prevent unbounded DB queries (MEDIUM-1).
        int page = query.Page;
        int limit = Math.Min(query.Limit, 100);

        PaginationRequest pagination = new()
        {
            Page = page,
            Limit = limit,
        };

        SearchCriteria criteria = new()
        {
            Query = query.Q,
            TypeFilter = query.TypeFilter,
            LanguageFilter = query.LanguageFilter,
            UseCaseFilter = query.UseCaseFilter,
        };

        (IReadOnlyList<SearchResultDto> items, int totalCount) =
            await _index.SearchAsync(criteria, pagination, ct);

        PaginatedEnvelope<SearchResultDto> envelope = new()
        {
            Data = items,
            TotalCount = totalCount,
            Page = page,
            Limit = limit,
        };

        IReadOnlyList<string> categorySuggestions = items.Count == 0
            ? DefaultCategorySuggestions
            : [];

        return new SearchPluginsResult
        {
            Envelope = envelope,
            CategorySuggestions = categorySuggestions,
        };
    }
}
