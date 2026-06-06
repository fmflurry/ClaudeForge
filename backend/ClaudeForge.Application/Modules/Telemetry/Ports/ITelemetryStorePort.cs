namespace ClaudeForge.Application.Modules.Telemetry.Ports;

/// <summary>
/// Outgoing port for telemetry storage operations.
/// Implementations must atomically record events and maintain aggregate counters.
/// </summary>
public interface ITelemetryStorePort
{
    /// <summary>
    /// Records a raw telemetry event and atomically upserts the corresponding
    /// daily aggregate bucket (pluginId, version, eventType, today UTC) with count += 1.
    /// </summary>
    Task RecordEventAsync(TelemetryEvent ev, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregated telemetry for the given plugin.
    /// Reads telemetry_aggregates only — raw events are never accessed.
    /// </summary>
    Task<TelemetrySummaryDto> GetSummaryAsync(Guid pluginId, CancellationToken ct = default);

    /// <summary>
    /// Deletes raw telemetry_events rows older than <paramref name="days"/> days.
    /// Does NOT touch telemetry_aggregates.
    /// </summary>
    /// <returns>Number of rows deleted.</returns>
    Task<int> PurgeRawEventsOlderThanAsync(int days, CancellationToken ct = default);
}
