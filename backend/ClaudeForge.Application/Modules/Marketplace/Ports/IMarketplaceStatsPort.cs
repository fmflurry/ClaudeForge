namespace ClaudeForge.Application.Modules.Marketplace.Ports;

/// <summary>
/// Outgoing port for marketplace statistics queries.
/// Implementations compute aggregate counts from public plugin data.
/// </summary>
public interface IMarketplaceStatsPort
{
    /// <summary>
    /// Returns aggregated marketplace statistics:
    /// total public plugins, total downloads, distinct publishers, and category count.
    /// </summary>
    Task<MarketplaceStatsDto> GetStatsAsync(CancellationToken ct = default);
}
