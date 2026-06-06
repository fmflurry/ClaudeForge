using ClaudeForge.Application.Modules.Docs.Ports;

namespace ClaudeForge.Application.Modules.Docs.UseCases;

/// <summary>
/// Retrieves a single documentation page by slug.
/// Throws DocNotFoundException (404) when the slug is not found.
/// </summary>
public sealed class GetDocPageUseCase
{
    private readonly IDocsRepositoryPort _repo;

    public GetDocPageUseCase(IDocsRepositoryPort repo)
    {
        _repo = repo;
    }

    public async Task<DocPageDto> ExecuteAsync(string slug, CancellationToken ct = default)
    {
        DocPageDto? page = await _repo.GetBySlugAsync(slug, ct);

        if (page is null)
        {
            throw new DocNotFoundException();
        }

        return page;
    }
}
