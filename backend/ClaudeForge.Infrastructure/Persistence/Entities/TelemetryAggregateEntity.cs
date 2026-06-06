namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>telemetry_aggregates</c> daily-summary table.
/// Composite PK: (plugin_id, version, event_type, window_start).
/// Source of truth for all telemetry counters exposed via the API.
/// </summary>
public sealed class TelemetryAggregateEntity
{
    public Guid PluginId { get; set; }

    /// <summary>
    /// Semantic version string. Empty string (<c>''</c>) represents a rollup across all versions.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Event type: <c>'download'</c> or <c>'install'</c>.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    public long Count { get; set; }

    /// <summary>
    /// Start of the daily aggregation window (date only).
    /// </summary>
    public DateOnly WindowStart { get; set; }
}
