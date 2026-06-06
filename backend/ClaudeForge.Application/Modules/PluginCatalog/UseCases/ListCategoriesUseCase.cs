using ClaudeForge.Application.Modules.PluginCatalog.Ports;

namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Returns all controlled-vocabulary categories grouped by dimension,
/// each annotated with the current plugin count.
/// </summary>
public sealed class ListCategoriesUseCase
{
    private readonly ICategoryRepositoryPort _repository;

    public ListCategoriesUseCase(ICategoryRepositoryPort repository)
    {
        _repository = repository;
    }

    public Task<CategoryListDto> ExecuteAsync(CancellationToken ct = default)
    {
        return _repository.GetAllCategoriesAsync(ct);
    }
}
