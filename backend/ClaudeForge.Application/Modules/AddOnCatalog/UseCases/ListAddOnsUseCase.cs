using ClaudeForge.Application.Modules.AddOnCatalog.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.AddOnCatalog.UseCases;

/// <summary>
/// Queries the plugin catalog with pagination, sorting, and category filtering.
/// Applies viewer-org-ids filtering so private plugins are excluded for callers
/// who are not members of the owning organization.
///
/// Validation rules (spec §3/§5):
/// - Invalid sort key → silently falls back to "createdAt" (never throws).
/// - Invalid type value → throws <see cref="InvalidCategoryException"/> with spec-exact message.
/// - Invalid language value → throws <see cref="InvalidCategoryException"/> with spec-exact message.
/// - Empty language array value (empty string) → throws <see cref="InvalidCategoryException"/>.
/// </summary>
public sealed class ListAddOnsUseCase
{
    private static readonly HashSet<string> ValidSortKeys =
        new(StringComparer.OrdinalIgnoreCase) { "downloads", "createdAt", "name" };

    private static readonly HashSet<string> ValidTypes =
        new(StringComparer.OrdinalIgnoreCase) { "skill", "hook", "plugin", "command", "agent" };

    private static readonly HashSet<string> ValidLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "typescript", "python", "go", "rust" };

    private readonly IAddOnRepositoryPort _repository;
    private readonly ICurrentUser? _currentUser;
    private readonly IOrgMembershipQueryPort? _membershipQuery;

    /// <summary>
    /// Full constructor for production use with viewerOrgIds filtering.
    /// </summary>
    public ListAddOnsUseCase(
        IAddOnRepositoryPort repository,
        ICurrentUser currentUser,
        IOrgMembershipQueryPort membershipQuery)
    {
        _repository = repository;
        _currentUser = currentUser;
        _membershipQuery = membershipQuery;
    }

    /// <summary>
    /// Backward-compatible constructor for unit tests and contexts without identity.
    /// Behaves as anonymous caller (public plugins only).
    /// </summary>
    public ListAddOnsUseCase(IAddOnRepositoryPort repository)
    {
        _repository = repository;
        _currentUser = null;
        _membershipQuery = null;
    }

    public async Task<PaginatedEnvelope<AddOnSummaryDto>> ExecuteAsync(
        ListAddOnsQuery query,
        CancellationToken ct = default)
    {
        ValidateCategories(query);

        string safeSortKey = ValidSortKeys.Contains(query.SortKey) ? query.SortKey : "createdAt";

        PaginationRequest pagination = new() { Page = query.Page, Limit = query.Limit };

        IReadOnlyList<AddOnSummaryDto> items;
        int totalCount;

        if (_currentUser is not null && _membershipQuery is not null)
        {
            // Full production path: resolve viewer org IDs and apply visibility filter
            IReadOnlySet<Guid> viewerOrgIds = await ResolveViewerOrgIdsAsync(ct);
            (items, totalCount) = await _repository.ListAddOnsAsync(
                pagination,
                safeSortKey,
                query.SortOrder,
                query.TypeFilter,
                query.LanguageFilter,
                query.UseCaseFilter,
                viewerOrgIds,
                ct);
        }
        else
        {
            // Backward-compat path (anonymous / unit-test mode): call legacy overload
            (items, totalCount) = await _repository.ListAddOnsAsync(
                pagination,
                safeSortKey,
                query.SortOrder,
                query.TypeFilter,
                query.LanguageFilter,
                query.UseCaseFilter,
                ct);
        }

        return new PaginatedEnvelope<AddOnSummaryDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = query.Page,
            Limit = query.Limit,
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

    private static void ValidateCategories(ListAddOnsQuery query)
    {
        if (query.TypeFilter is not null)
        {
            foreach (string type in query.TypeFilter)
            {
                if (!ValidTypes.Contains(type))
                {
                    throw new InvalidCategoryException(
                        "Type must be one of: skill, hook, plugin, command, agent");
                }
            }
        }

        if (query.LanguageFilter is not null)
        {
            foreach (string language in query.LanguageFilter)
            {
                if (string.IsNullOrEmpty(language))
                {
                    throw new InvalidCategoryException("At least one language must be specified");
                }

                if (!ValidLanguages.Contains(language))
                {
                    throw new InvalidCategoryException(
                        $"language '{language}' is not a valid category value");
                }
            }
        }
    }
}
