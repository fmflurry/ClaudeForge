using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Queries the plugin catalog with pagination, sorting, and category filtering.
///
/// Validation rules (spec §3/§5):
/// - Invalid sort key → silently falls back to "createdAt" (never throws).
/// - Invalid type value → throws <see cref="InvalidCategoryException"/> with spec-exact message.
/// - Invalid language value → throws <see cref="InvalidCategoryException"/> with spec-exact message.
/// - Empty language array value (empty string) → throws <see cref="InvalidCategoryException"/>.
/// </summary>
public sealed class ListPluginsUseCase
{
    private static readonly HashSet<string> ValidSortKeys =
        new(StringComparer.OrdinalIgnoreCase) { "downloads", "createdAt", "name" };

    private static readonly HashSet<string> ValidTypes =
        new(StringComparer.OrdinalIgnoreCase) { "skill", "hook", "plugin", "command", "agent" };

    private static readonly HashSet<string> ValidLanguages =
        new(StringComparer.OrdinalIgnoreCase) { "typescript", "python", "go", "rust" };

    private readonly IPluginRepositoryPort _repository;

    public ListPluginsUseCase(IPluginRepositoryPort repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedEnvelope<PluginSummaryDto>> ExecuteAsync(
        ListPluginsQuery query,
        CancellationToken ct = default)
    {
        ValidateCategories(query);

        string safeSortKey = ValidSortKeys.Contains(query.SortKey) ? query.SortKey : "createdAt";

        PaginationRequest pagination = new() { Page = query.Page, Limit = query.Limit };

        (IReadOnlyList<PluginSummaryDto> items, int totalCount) = await _repository.ListPluginsAsync(
            pagination,
            safeSortKey,
            query.SortOrder,
            query.TypeFilter,
            query.LanguageFilter,
            query.UseCaseFilter,
            ct);

        return new PaginatedEnvelope<PluginSummaryDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = query.Page,
            Limit = query.Limit,
        };
    }

    private static void ValidateCategories(ListPluginsQuery query)
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
