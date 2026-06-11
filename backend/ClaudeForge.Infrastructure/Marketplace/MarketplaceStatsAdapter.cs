using ClaudeForge.Application.Modules.Marketplace.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Marketplace;

/// <summary>
/// EF Core adapter implementing <see cref="IMarketplaceStatsPort"/>.
/// Computes aggregated marketplace statistics over public plugins only.
/// Private plugins (visibility != 'public') are excluded from all aggregates.
/// </summary>
public sealed class MarketplaceStatsAdapter : IMarketplaceStatsPort
{
    private const string PublicVisibility = "public";

    private readonly MarketplaceDbContext _context;

    public MarketplaceStatsAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<MarketplaceStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        IQueryable<AddOnEntity> publicPlugins =
            _context.Plugins
                .AsNoTracking()
                .Where(p => p.Visibility == PublicVisibility);

        long totalPlugins = await publicPlugins.LongCountAsync(ct);

        long totalDownloads = totalPlugins == 0L
            ? 0L
            : await publicPlugins.SumAsync(p => p.DownloadCount, ct);

        long publisherCount = totalPlugins == 0L
            ? 0L
            : await publicPlugins.Select(p => p.Author).Distinct().LongCountAsync(ct);

        long categoryCount = await _context.Categories
            .AsNoTracking()
            .LongCountAsync(ct);

        return new MarketplaceStatsDto
        {
            TotalPlugins = totalPlugins,
            TotalDownloads = totalDownloads,
            PublisherCount = publisherCount,
            CategoryCount = categoryCount,
        };
    }
}
