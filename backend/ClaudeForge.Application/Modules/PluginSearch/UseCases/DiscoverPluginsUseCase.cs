using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Use case for discovering plugins using a mandatory keyword and optional category filters.
/// Validates keyword (blank → BlankKeywordException), delegates to ISearchIndexPort,
/// sorts results by relevance descending, and echoes criteria when no results are found.
/// Applies viewerOrgIds filtering so private plugins invisible to the caller are excluded.
/// </summary>
public sealed class DiscoverPluginsUseCase
{
    private readonly ISearchIndexPort _index;
    private readonly ICurrentUser? _currentUser;
    private readonly IOrgMembershipQueryPort? _membershipQuery;

    /// <summary>
    /// Full constructor for production use with viewerOrgIds filtering.
    /// </summary>
    public DiscoverPluginsUseCase(
        ISearchIndexPort index,
        ICurrentUser currentUser,
        IOrgMembershipQueryPort membershipQuery)
    {
        _index = index;
        _currentUser = currentUser;
        _membershipQuery = membershipQuery;
    }

    /// <summary>
    /// Backward-compatible constructor for unit tests and contexts without identity.
    /// Behaves as anonymous caller (public plugins only).
    /// </summary>
    public DiscoverPluginsUseCase(ISearchIndexPort index)
    {
        _index = index;
        _currentUser = null;
        _membershipQuery = null;
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

        IReadOnlyList<DiscoveryResultDto> items;

        if (_currentUser is not null && _membershipQuery is not null)
        {
            IReadOnlySet<Guid> viewerOrgIds = await ResolveViewerOrgIdsAsync(ct);
            (items, _) = await _index.DiscoverAsync(criteria, viewerOrgIds, ct);
        }
        else
        {
            // Backward-compat path (unit-test mode): call legacy 2-arg overload
            (items, _) = await _index.DiscoverAsync(criteria, ct);
        }

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

    private async Task<IReadOnlySet<Guid>> ResolveViewerOrgIdsAsync(CancellationToken ct)
    {
        if (_currentUser is null || _membershipQuery is null ||
            !_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return new HashSet<Guid>();
        }

        Guid[] orgIds = await _membershipQuery.GetOrgIdsForUserAsync(_currentUser.UserId.Value, ct);
        return new HashSet<Guid>(orgIds);
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
