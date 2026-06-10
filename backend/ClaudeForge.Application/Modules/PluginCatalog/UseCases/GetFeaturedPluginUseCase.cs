using ClaudeForge.Application.Modules.PluginCatalog.Ports;

namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Returns the currently featured plugin as a <see cref="FeaturedPluginDto"/>,
/// or <c>null</c> when no plugin is flagged as featured.
/// Callers should map <c>null</c> to HTTP 404.
/// </summary>
public sealed class GetFeaturedPluginUseCase
{
    private readonly IPluginRepositoryPort _repository;

    public GetFeaturedPluginUseCase(IPluginRepositoryPort repository)
    {
        _repository = repository;
    }

    /// <returns>
    /// The featured plugin summary, or <c>null</c> when none is featured.
    /// </returns>
    public Task<FeaturedPluginDto?> ExecuteAsync(CancellationToken ct = default)
    {
        return _repository.GetFeaturedPluginAsync(ct);
    }
}
