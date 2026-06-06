namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// DTO grouping all categories by their three controlled-vocabulary dimensions.
/// </summary>
public sealed record CategoryListDto
{
    public required IReadOnlyList<CategoryDto> Types { get; init; }
    public required IReadOnlyList<CategoryDto> Languages { get; init; }
    public required IReadOnlyList<CategoryDto> UseCases { get; init; }
}
