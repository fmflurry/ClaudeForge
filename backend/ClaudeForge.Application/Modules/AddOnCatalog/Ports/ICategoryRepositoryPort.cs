using ClaudeForge.Application.Modules.AddOnCatalog.UseCases;

namespace ClaudeForge.Application.Modules.AddOnCatalog.Ports;

/// <summary>
/// Port for category data access operations.
/// Implemented by the infrastructure adapter (<c>AddOnRepositoryAdapter</c>).
/// </summary>
public interface ICategoryRepositoryPort
{
    /// <summary>
    /// Returns all categories grouped by their three controlled-vocabulary dimensions,
    /// each annotated with the number of plugins currently tagged with that value.
    /// </summary>
    Task<CategoryListDto> GetAllCategoriesAsync(CancellationToken ct = default);
}
