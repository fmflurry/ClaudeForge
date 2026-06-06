using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.PluginCatalog.Ports;

/// <summary>
/// Port for plugin data access operations.
/// Implemented by the infrastructure adapter (<c>PluginRepositoryAdapter</c>).
/// </summary>
public interface IPluginRepositoryPort
{
    /// <summary>
    /// Returns a paginated, filtered, and sorted list of plugin summaries plus the total count.
    /// </summary>
    Task<(IReadOnlyList<PluginSummaryDto> Items, int TotalCount)> ListPluginsAsync(
        PaginationRequest pagination,
        string sortKey,
        string sortOrder,
        IReadOnlyList<string>? typeFilter,
        IReadOnlyList<string>? languageFilter,
        IReadOnlyList<string>? useCaseFilter,
        CancellationToken ct = default);

    /// <summary>
    /// Returns full plugin details including version history, or <c>null</c> if not found.
    /// </summary>
    Task<PluginDetailDto?> GetPluginByIdAsync(Guid pluginId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when a plugin with the given normalized name already exists.
    /// The check is case-insensitive: callers must pass <c>name.ToLowerInvariant()</c>.
    /// </summary>
    Task<bool> ExistsByNameNormalizedAsync(string nameNormalized, CancellationToken ct = default);
}
