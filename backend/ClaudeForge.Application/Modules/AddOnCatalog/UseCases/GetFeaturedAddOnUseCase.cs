using ClaudeForge.Application.Modules.AddOnCatalog.Ports;

namespace ClaudeForge.Application.Modules.AddOnCatalog.UseCases;

/// <summary>
/// Returns the currently featured plugin as a <see cref="FeaturedAddOnDto"/>,
/// or <c>null</c> when no plugin is flagged as featured.
/// Callers should map <c>null</c> to HTTP 404.
/// </summary>
public sealed class GetFeaturedAddOnUseCase
{
    private readonly IAddOnRepositoryPort _repository;

    public GetFeaturedAddOnUseCase(IAddOnRepositoryPort repository)
    {
        _repository = repository;
    }

    /// <returns>
    /// The featured plugin summary, or <c>null</c> when none is featured.
    /// </returns>
    public Task<FeaturedAddOnDto?> ExecuteAsync(CancellationToken ct = default)
    {
        return _repository.GetFeaturedAddOnAsync(ct);
    }
}
