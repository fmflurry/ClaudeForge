using ClaudeForge.Application.Modules.Marketplace.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace ClaudeForge.Application.Modules.Marketplace.UseCases;

/// <summary>
/// Returns aggregated marketplace statistics, backed by a 5-minute in-memory cache.
/// Only public aggregate counts are returned — no per-plugin detail or PII.
/// </summary>
public sealed class GetMarketplaceStatsUseCase
{
    private const string CacheKey = "marketplace-stats";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IMarketplaceStatsPort _port;
    private readonly IMemoryCache _cache;

    public GetMarketplaceStatsUseCase(IMarketplaceStatsPort port, IMemoryCache cache)
    {
        _port = port;
        _cache = cache;
    }

    public async Task<MarketplaceStatsDto> ExecuteAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out MarketplaceStatsDto? cached) && cached is not null)
        {
            return cached;
        }

        MarketplaceStatsDto stats = await _port.GetStatsAsync(ct);

        MemoryCacheEntryOptions options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration);

        _cache.Set(CacheKey, stats, options);

        return stats;
    }
}
