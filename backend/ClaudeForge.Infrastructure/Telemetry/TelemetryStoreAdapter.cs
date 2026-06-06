using ClaudeForge.Application.Modules.Telemetry.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Telemetry;

/// <summary>
/// EF Core adapter implementing <see cref="ITelemetryStorePort"/>.
/// RecordEventAsync atomically inserts a raw event row and upserts the daily aggregate bucket.
/// GetSummaryAsync reads telemetry_aggregates only — raw events are never accessed.
/// </summary>
public sealed class TelemetryStoreAdapter : ITelemetryStorePort
{
    private readonly MarketplaceDbContext _context;

    public TelemetryStoreAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task RecordEventAsync(TelemetryEvent ev, CancellationToken ct = default)
    {
        string version = ev.Version ?? string.Empty;
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx =
            await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // 1. INSERT raw telemetry_events row
            TelemetryEventEntity entity = new()
            {
                EventType = ev.EventType,
                PluginId = ev.PluginId,
                Version = ev.Version,
                AnonClientId = ev.AnonClientId,
                ClientOs = ev.ClientOs,
                ClientArch = ev.ClientArch,
                OccurredAt = ev.OccurredAt,
            };
            _context.TelemetryEvents.Add(entity);
            await _context.SaveChangesAsync(ct);

            // 2. UPSERT telemetry_aggregates: atomic DB-side count += 1 on conflict
            await _context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO telemetry_aggregates (plugin_id, version, event_type, window_start, count)
                VALUES ({0}, {1}, {2}, {3}, 1)
                ON CONFLICT (plugin_id, version, event_type, window_start)
                DO UPDATE SET count = telemetry_aggregates.count + 1
                """,
                [ev.PluginId, version, ev.EventType, today],
                ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<TelemetrySummaryDto> GetSummaryAsync(Guid pluginId, CancellationToken ct = default)
    {
        DateOnly sevenDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));

        // Read all aggregate rows for this plugin
        List<TelemetryAggregateEntity> rows = await _context.TelemetryAggregates
            .AsNoTracking()
            .Where(a => a.PluginId == pluginId)
            .ToListAsync(ct);

        long totalDownloads = rows
            .Where(a => a.EventType == "download")
            .Sum(a => a.Count);

        long totalInstalls = rows
            .Where(a => a.EventType == "install")
            .Sum(a => a.Count);

        // Last 7 days breakdown: group by (date, eventType), then pivot
        IReadOnlyList<DailyActivityDto> last7Days = rows
            .Where(a => a.WindowStart >= sevenDaysAgo)
            .GroupBy(a => a.WindowStart)
            .Select(g => new DailyActivityDto
            {
                Date = g.Key,
                Downloads = g.Where(a => a.EventType == "download").Sum(a => a.Count),
                Installs = g.Where(a => a.EventType == "install").Sum(a => a.Count),
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new TelemetrySummaryDto
        {
            PluginId = pluginId,
            TotalDownloads = totalDownloads,
            TotalInstalls = totalInstalls,
            Last7Days = last7Days,
        };
    }

    /// <inheritdoc/>
    public async Task<int> PurgeRawEventsOlderThanAsync(int days, CancellationToken ct = default)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        // DELETE raw events older than cutoff; does NOT touch telemetry_aggregates
        return await _context.TelemetryEvents
            .Where(e => e.OccurredAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
