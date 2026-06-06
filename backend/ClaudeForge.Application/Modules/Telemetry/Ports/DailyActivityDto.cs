namespace ClaudeForge.Application.Modules.Telemetry.Ports;

/// <summary>
/// Aggregated telemetry activity for a single day.
/// Contains only aggregate counts — no PII or raw event data.
/// </summary>
public sealed record DailyActivityDto
{
    public required DateOnly Date { get; init; }
    public required long Downloads { get; init; }
    public required long Installs { get; init; }
}
