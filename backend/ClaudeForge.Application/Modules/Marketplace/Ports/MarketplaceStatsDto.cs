namespace ClaudeForge.Application.Modules.Marketplace.Ports;

/// <summary>
/// Aggregated marketplace statistics.
/// Contains only aggregate counts — no per-plugin detail, raw events, or PII.
/// </summary>
public sealed record MarketplaceStatsDto
{
    public required long TotalPlugins { get; init; }
    public required long TotalDownloads { get; init; }
    public required long PublisherCount { get; init; }
    public required long CategoryCount { get; init; }
}
