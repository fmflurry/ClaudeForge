using ClaudeForge.Application.Modules.PluginSearch.Ports;

namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Use case for discovering plugins using a mandatory keyword and optional category filters.
/// Validates keyword (blank → BlankKeywordException), delegates to ISearchIndexPort,
/// sorts results by relevance descending, and echoes criteria when no results are found.
/// </summary>
public sealed class DiscoverPluginsUseCase
{
    private readonly ISearchIndexPort _index;

    public DiscoverPluginsUseCase(ISearchIndexPort index)
    {
        _index = index;
    }

    public async Task<DiscoverPluginsResult> ExecuteAsync(
        DiscoverPluginsQuery query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Keyword))
        {
            throw new BlankKeywordException();
        }

        SearchCriteria criteria = new()
        {
            Query = query.Keyword,
            TypeFilter = query.TypeFilter,
            LanguageFilter = query.LanguageFilter,
            UseCaseFilter = query.UseCaseFilter,
        };

        (IReadOnlyList<DiscoveryResultDto> items, _) =
            await _index.DiscoverAsync(criteria, ct);

        // Sort by relevance descending
        IReadOnlyList<DiscoveryResultDto> sorted = [.. items.OrderByDescending(i => i.RelevanceScore)];

        IReadOnlyList<string> criteriaEchoed = sorted.Count == 0
            ? BuildCriteriaEcho(query)
            : [];

        return new DiscoverPluginsResult
        {
            Items = sorted,
            CriteriaEchoed = criteriaEchoed,
        };
    }

    private static IReadOnlyList<string> BuildCriteriaEcho(DiscoverPluginsQuery query)
    {
        List<string> echoed = [];

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            echoed.Add($"keyword: {query.Keyword}");
        }

        if (query.LanguageFilter is { Count: > 0 })
        {
            echoed.Add($"language: {string.Join(", ", query.LanguageFilter)}");
        }

        if (query.UseCaseFilter is { Count: > 0 })
        {
            echoed.Add($"useCase: {string.Join(", ", query.UseCaseFilter)}");
        }

        if (query.TypeFilter is { Count: > 0 })
        {
            echoed.Add($"type: {string.Join(", ", query.TypeFilter)}");
        }

        return echoed;
    }
}
