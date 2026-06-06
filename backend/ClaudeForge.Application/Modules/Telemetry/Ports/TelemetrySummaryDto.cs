namespace ClaudeForge.Application.Modules.Telemetry.Ports;

/// <summary>
/// Aggregated telemetry summary for a plugin.
/// Read from telemetry_aggregates only — raw events are never exposed.
/// </summary>
public sealed record TelemetrySummaryDto
{
    public required Guid PluginId { get; init; }
    public required long TotalDownloads { get; init; }
    public required long TotalInstalls { get; init; }
    public required IReadOnlyList<DailyActivityDto> Last7Days { get; init; }
}
