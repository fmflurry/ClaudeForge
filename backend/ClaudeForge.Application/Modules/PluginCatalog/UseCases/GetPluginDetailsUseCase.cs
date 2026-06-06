using ClaudeForge.Application.Modules.PluginCatalog.Ports;

namespace ClaudeForge.Application.Modules.PluginCatalog.UseCases;

/// <summary>
/// Retrieves full plugin details including version history.
/// Throws <see cref="PluginNotFoundException"/> when the plugin does not exist.
/// </summary>
public sealed class GetPluginDetailsUseCase
{
    private readonly IPluginRepositoryPort _repository;

    public GetPluginDetailsUseCase(IPluginRepositoryPort repository)
    {
        _repository = repository;
    }

    public async Task<PluginDetailDto> ExecuteAsync(Guid pluginId, CancellationToken ct = default)
    {
        PluginDetailDto? detail = await _repository.GetPluginByIdAsync(pluginId, ct);

        if (detail is null)
        {
            throw new PluginNotFoundException();
        }

        return detail;
    }
}
