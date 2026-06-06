using ClaudeForge.Application.Modules.Telemetry.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace ClaudeForge.Application.Modules.Telemetry.UseCases;

/// <summary>
/// Returns aggregated telemetry for a plugin, backed by a 5-minute in-memory cache.
/// Reads aggregates only — raw events are never exposed.
/// </summary>
public sealed class GetTelemetrySummaryUseCase
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ITelemetryStorePort _store;
    private readonly IMemoryCache _cache;

    public GetTelemetrySummaryUseCase(ITelemetryStorePort store, IMemoryCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<TelemetrySummaryDto> ExecuteAsync(Guid pluginId, CancellationToken ct = default)
    {
        string cacheKey = $"telemetry-summary:{pluginId}";

        if (_cache.TryGetValue(cacheKey, out TelemetrySummaryDto? cached) && cached is not null)
        {
            return cached;
        }

        TelemetrySummaryDto summary = await _store.GetSummaryAsync(pluginId, ct);

        MemoryCacheEntryOptions options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration);

        _cache.Set(cacheKey, summary, options);

        return summary;
    }
}
