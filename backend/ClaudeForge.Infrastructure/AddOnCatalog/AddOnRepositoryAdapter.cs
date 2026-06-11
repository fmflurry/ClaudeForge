using ClaudeForge.Application.Modules.AddOnCatalog.Ports;
using ClaudeForge.Application.Modules.AddOnCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.AddOnCatalog;

/// <summary>
/// EF Core adapter implementing both <see cref="IAddOnRepositoryPort"/> and
/// <see cref="ICategoryRepositoryPort"/>.
/// </summary>
public sealed class AddOnRepositoryAdapter : IAddOnRepositoryPort, ICategoryRepositoryPort
{
    private readonly MarketplaceDbContext _context;

    public AddOnRepositoryAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // IAddOnRepositoryPort
    // -------------------------------------------------------------------------

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<AddOnSummaryDto> Items, int TotalCount)> ListAddOnsAsync(
        PaginationRequest pagination,
        string sortKey,
        string sortOrder,
        IReadOnlyList<string>? typeFilter,
        IReadOnlyList<string>? languageFilter,
        IReadOnlyList<string>? useCaseFilter,
        CancellationToken ct = default)
        => ListAddOnsAsync(pagination, sortKey, sortOrder, typeFilter, languageFilter, useCaseFilter,
            new HashSet<Guid>(), ct);

    public async Task<(IReadOnlyList<AddOnSummaryDto> Items, int TotalCount)> ListAddOnsAsync(
        PaginationRequest pagination,
        string sortKey,
        string sortOrder,
        IReadOnlyList<string>? typeFilter,
        IReadOnlyList<string>? languageFilter,
        IReadOnlyList<string>? useCaseFilter,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        IQueryable<AddOnEntity> query = _context.Plugins
            .Include(p => p.PluginCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Versions);

        // Visibility filter: public OR member of owning org
        query = ApplyVisibilityFilter(query, viewerOrgIds);

        // Category filter: AND across dimensions, OR within a dimension
        if (typeFilter is { Count: > 0 })
        {
            query = query.Where(p =>
                p.PluginCategories.Any(pc =>
                    pc.Category.Dimension == "type" &&
                    typeFilter.Contains(pc.Category.Value)));
        }

        if (languageFilter is { Count: > 0 })
        {
            query = query.Where(p =>
                p.PluginCategories.Any(pc =>
                    pc.Category.Dimension == "language" &&
                    languageFilter.Contains(pc.Category.Value)));
        }

        if (useCaseFilter is { Count: > 0 })
        {
            query = query.Where(p =>
                p.PluginCategories.Any(pc =>
                    pc.Category.Dimension == "use_case" &&
                    useCaseFilter.Contains(pc.Category.Value)));
        }

        // Count AFTER visibility + category filter so totalCount excludes hidden items
        int totalCount = await query.CountAsync(ct);

        query = ApplySort(query, sortKey, sortOrder);

        int skip = (pagination.Page - 1) * pagination.Limit;
        List<AddOnEntity> entities = await query
            .Skip(skip)
            .Take(pagination.Limit)
            .AsNoTracking()
            .ToListAsync(ct);

        IReadOnlyList<AddOnSummaryDto> items = entities
            .Select(MapToSummary)
            .ToList();

        return (items, totalCount);
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<AddOnDetailDto?> GetAddOnByIdAsync(Guid pluginId, CancellationToken ct = default)
        => GetAddOnByIdAsync(pluginId, new HashSet<Guid>(), ct);

    public async Task<AddOnDetailDto?> GetAddOnByIdAsync(
        Guid pluginId,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        IQueryable<AddOnEntity> query = _context.Plugins
            .Include(p => p.PluginCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Versions)
            .AsNoTracking()
            .Where(p => p.Id == pluginId);

        // Apply visibility filter — returns null for private plugins the caller cannot see
        query = ApplyVisibilityFilter(query, viewerOrgIds);

        AddOnEntity? entity = await query.FirstOrDefaultAsync(ct);

        return entity is null ? null : MapToDetail(entity);
    }

    public Task<bool> ExistsByNameNormalizedAsync(string nameNormalized, CancellationToken ct = default)
    {
        string lower = nameNormalized.ToLowerInvariant();
        return _context.Plugins.AnyAsync(p => p.NameNormalized == lower, ct);
    }

    public async Task<FeaturedAddOnDto?> GetFeaturedAddOnAsync(CancellationToken ct = default)
    {
        AddOnEntity? entity = await _context.Plugins
            .Include(p => p.Versions)
            .AsNoTracking()
            .Where(p => p.IsFeatured)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
        {
            return null;
        }

        string? latestVersion = entity.Versions
            .FirstOrDefault(v => v.IsLatest)?.Version;

        return new FeaturedAddOnDto
        {
            PluginId = entity.Id.ToString(),
            Name = entity.Name,
            Slug = entity.Slug,
            LatestVersion = latestVersion,
        };
    }

    // -------------------------------------------------------------------------
    // ICategoryRepositoryPort
    // -------------------------------------------------------------------------

    public async Task<CategoryListDto> GetAllCategoriesAsync(CancellationToken ct = default)
    {
        List<CategoryEntity> categories = await _context.Categories
            .Include(c => c.PluginCategories)
            .AsNoTracking()
            .ToListAsync(ct);

        IReadOnlyList<CategoryDto> types = categories
            .Where(c => c.Dimension == "type")
            .Select(c => new CategoryDto
            {
                Value = c.Value,
                DisplayName = c.DisplayName,
                Description = c.Description,
                Count = c.PluginCategories.Count,
            })
            .ToList();

        IReadOnlyList<CategoryDto> languages = categories
            .Where(c => c.Dimension == "language")
            .Select(c => new CategoryDto
            {
                Value = c.Value,
                DisplayName = c.DisplayName,
                Description = c.Description,
                Count = c.PluginCategories.Count,
            })
            .ToList();

        IReadOnlyList<CategoryDto> useCases = categories
            .Where(c => c.Dimension == "use_case")
            .Select(c => new CategoryDto
            {
                Value = c.Value,
                DisplayName = c.DisplayName,
                Description = c.Description,
                Count = c.PluginCategories.Count,
            })
            .ToList();

        return new CategoryListDto
        {
            Types = types,
            Languages = languages,
            UseCases = useCases,
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies the visibility predicate:
    ///   visibility='public' OR owner_org_id = ANY(viewerOrgIds)
    /// When <paramref name="viewerOrgIds"/> is empty, only public plugins pass.
    /// </summary>
    private static IQueryable<AddOnEntity> ApplyVisibilityFilter(
        IQueryable<AddOnEntity> query,
        IReadOnlySet<Guid> viewerOrgIds)
    {
        if (viewerOrgIds.Count == 0)
        {
            return query.Where(p => p.Visibility == "public");
        }

        // EF Core translates this to:
        //   WHERE visibility = 'public' OR owner_org_id = ANY(@viewerOrgIds)
        Guid[] viewerOrgIdsArray = viewerOrgIds.ToArray();
        return query.Where(p =>
            p.Visibility == "public" ||
            (p.OwnerOrgId != null && viewerOrgIdsArray.Contains(p.OwnerOrgId.Value)));
    }

    private static IQueryable<AddOnEntity> ApplySort(
        IQueryable<AddOnEntity> query,
        string sortKey,
        string sortOrder)
    {
        bool descending = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        return sortKey.ToLowerInvariant() switch
        {
            "downloads" => descending
                ? query.OrderByDescending(p => p.DownloadCount)
                : query.OrderBy(p => p.DownloadCount),
            "name" => descending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
            _ => descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
        };
    }

    private static AddOnSummaryDto MapToSummary(AddOnEntity entity)
    {
        string? latestVersion = entity.Versions
            .FirstOrDefault(v => v.IsLatest)?.Version;

        IReadOnlyList<string> types = entity.PluginCategories
            .Where(pc => pc.Category.Dimension == "type")
            .Select(pc => pc.Category.Value)
            .ToList();

        IReadOnlyList<string> languages = entity.PluginCategories
            .Where(pc => pc.Category.Dimension == "language")
            .Select(pc => pc.Category.Value)
            .ToList();

        IReadOnlyList<string> useCases = entity.PluginCategories
            .Where(pc => pc.Category.Dimension == "use_case")
            .Select(pc => pc.Category.Value)
            .ToList();

        return new AddOnSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            Description = entity.Description,
            Author = entity.Author,
            DownloadCount = entity.DownloadCount,
            LatestVersion = latestVersion,
            CreatedAt = entity.CreatedAt,
            Types = types,
            Languages = languages,
            UseCaseTags = useCases,
        };
    }

    private static AddOnDetailDto MapToDetail(AddOnEntity entity)
    {
        IReadOnlyList<AddOnVersionDto> versions = entity.Versions
            .OrderByDescending(v => v.VersionSort)
            .Select(v => new AddOnVersionDto
            {
                VersionNumber = v.Version,
                ReleaseDate = v.ReleasedAt,
                ReleaseNotes = v.ReleaseNotes,
                DownloadCount = v.DownloadCount,
                IsLatest = v.IsLatest,
            })
            .ToList();

        string? latestVersion = versions.FirstOrDefault(v => v.IsLatest)?.VersionNumber;

        IReadOnlyList<string> types = entity.PluginCategories
            .Where(pc => pc.Category.Dimension == "type")
            .Select(pc => pc.Category.Value)
            .ToList();

        IReadOnlyList<string> languages = entity.PluginCategories
            .Where(pc => pc.Category.Dimension == "language")
            .Select(pc => pc.Category.Value)
            .ToList();

        IReadOnlyList<string> useCases = entity.PluginCategories
            .Where(pc => pc.Category.Dimension == "use_case")
            .Select(pc => pc.Category.Value)
            .ToList();

        return new AddOnDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Slug = entity.Slug,
            Description = entity.Description,
            Author = entity.Author,
            DownloadCount = entity.DownloadCount,
            LatestVersion = latestVersion,
            CreatedAt = entity.CreatedAt,
            Types = types,
            Languages = languages,
            UseCaseTags = useCases,
            Versions = versions,
        };
    }
}
