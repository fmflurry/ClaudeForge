namespace ClaudeForge.Application.Modules.PluginSearch.UseCases;

/// <summary>
/// Result of the DiscoverPluginsUseCase.
/// CriteriaEchoed is populated only when Items is empty, echoing the applied criteria.
/// </summary>
public sealed record DiscoverPluginsResult
{
    public required IReadOnlyList<DiscoveryResultDto> Items { get; init; }
    public required IReadOnlyList<string> CriteriaEchoed { get; init; }
}
