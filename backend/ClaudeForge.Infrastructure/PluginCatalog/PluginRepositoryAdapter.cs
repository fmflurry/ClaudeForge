using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.PluginCatalog;

/// <summary>
/// EF Core adapter implementing both <see cref="IPluginRepositoryPort"/> and
/// <see cref="ICategoryRepositoryPort"/>.
/// </summary>
public sealed class PluginRepositoryAdapter : IPluginRepositoryPort, ICategoryRepositoryPort
{
    private readonly MarketplaceDbContext _context;

    public PluginRepositoryAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // IPluginRepositoryPort
    // -------------------------------------------------------------------------

    public async Task<(IReadOnlyList<PluginSummaryDto> Items, int TotalCount)> ListPluginsAsync(
        PaginationRequest pagination,
        string sortKey,
        string sortOrder,
        IReadOnlyList<string>? typeFilter,
        IReadOnlyList<string>? languageFilter,
        IReadOnlyList<string>? useCaseFilter,
        CancellationToken ct = default)
    {
        IQueryable<PluginEntity> query = _context.Plugins
            .Include(p => p.PluginCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Versions);

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

        int totalCount = await query.CountAsync(ct);

        query = ApplySort(query, sortKey, sortOrder);

        int skip = (pagination.Page - 1) * pagination.Limit;
        List<PluginEntity> entities = await query
            .Skip(skip)
            .Take(pagination.Limit)
            .AsNoTracking()
            .ToListAsync(ct);

        IReadOnlyList<PluginSummaryDto> items = entities
            .Select(MapToSummary)
            .ToList();

        return (items, totalCount);
    }

    public async Task<PluginDetailDto?> GetPluginByIdAsync(Guid pluginId, CancellationToken ct = default)
    {
        PluginEntity? entity = await _context.Plugins
            .Include(p => p.PluginCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Versions)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pluginId, ct);

        return entity is null ? null : MapToDetail(entity);
    }

    public Task<bool> ExistsByNameNormalizedAsync(string nameNormalized, CancellationToken ct = default)
    {
        string lower = nameNormalized.ToLowerInvariant();
        return _context.Plugins.AnyAsync(p => p.NameNormalized == lower, ct);
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

    private static IQueryable<PluginEntity> ApplySort(
        IQueryable<PluginEntity> query,
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

    private static PluginSummaryDto MapToSummary(PluginEntity entity)
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

        return new PluginSummaryDto
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
            UseCases = useCases,
        };
    }

    private static PluginDetailDto MapToDetail(PluginEntity entity)
    {
        IReadOnlyList<PluginVersionDto> versions = entity.Versions
            .OrderByDescending(v => v.VersionSort)
            .Select(v => new PluginVersionDto
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

        return new PluginDetailDto
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
            UseCases = useCases,
            Versions = versions,
        };
    }
}
