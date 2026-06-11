namespace ClaudeForge.Application.Modules.AddOnSearch.UseCases;

/// <summary>
/// Result of the DiscoverAddOnsUseCase.
/// CriteriaEchoed is populated only when Items is empty, echoing the applied criteria.
/// </summary>
public sealed record DiscoverAddOnsResult
{
    public required IReadOnlyList<DiscoveryResultDto> Items { get; init; }
    public required IReadOnlyList<string> CriteriaEchoed { get; init; }
}
