namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Summary DTO for the featured plugin, containing the minimum fields
/// needed by the landing-page showcase to compose the CLI install command.
/// </summary>
public sealed record FeaturedPluginDto
{
    public required string PluginId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string? LatestVersion { get; init; }
}
