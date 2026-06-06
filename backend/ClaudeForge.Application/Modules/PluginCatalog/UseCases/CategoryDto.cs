namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// DTO for a single controlled-vocabulary category entry with plugin count.
/// </summary>
public sealed record CategoryDto
{
    public required string Value { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public required int Count { get; init; }
}
