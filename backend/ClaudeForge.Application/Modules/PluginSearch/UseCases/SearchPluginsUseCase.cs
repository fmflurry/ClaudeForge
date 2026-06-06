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
        if (query.Page <= 0 || query.Limit <= 0)
        {
            throw new InvalidPaginationException();
        }

        SearchCriteria criteria = new()
        {
            Query = query.Q,
            TypeFilter = query.TypeFilter,
            LanguageFilter = query.LanguageFilter,
            UseCaseFilter = query.UseCaseFilter,
        };

        PaginationRequest pagination = new()
        {
            Page = query.Page,
            Limit = query.Limit,
        };

        (IReadOnlyList<SearchResultDto> items, int totalCount) =
            await _index.SearchAsync(criteria, pagination, ct);

        PaginatedEnvelope<SearchResultDto> envelope = new()
        {
            Data = items,
            TotalCount = totalCount,
            Page = query.Page,
            Limit = query.Limit,
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
