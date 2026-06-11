using ClaudeForge.Application.Modules.AddOnSearch.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.AddOnSearch.UseCases;

/// <summary>
/// Use case for searching plugins by full-text query and category filters.
/// Validates pagination, delegates to ISearchIndexPort, wraps results in PaginatedEnvelope.
/// When no results are found, populates CategorySuggestions.
/// Applies viewerOrgIds filtering so private plugins invisible to the caller are excluded.
/// </summary>
public sealed class SearchAddOnsUseCase
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
    private readonly ICurrentUser? _currentUser;
    private readonly IOrgMembershipQueryPort? _membershipQuery;

    /// <summary>
    /// Full constructor for production use with viewerOrgIds filtering.
    /// </summary>
    public SearchAddOnsUseCase(
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
    public SearchAddOnsUseCase(ISearchIndexPort index)
    {
        _index = index;
        _currentUser = null;
        _membershipQuery = null;
    }

    public async Task<SearchAddOnsResult> ExecuteAsync(
        SearchAddOnsQuery query,
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

        IReadOnlyList<SearchResultDto> items;
        int totalCount;

        if (_currentUser is not null && _membershipQuery is not null)
        {
            IReadOnlySet<Guid> viewerOrgIds = await ResolveViewerOrgIdsAsync(ct);
            (items, totalCount) = await _index.SearchAsync(criteria, pagination, viewerOrgIds, ct);
        }
        else
        {
            // Backward-compat path (unit-test mode): call legacy 3-arg overload
            (items, totalCount) = await _index.SearchAsync(criteria, pagination, ct);
        }

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

        return new SearchAddOnsResult
        {
            Envelope = envelope,
            CategorySuggestions = categorySuggestions,
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
}
