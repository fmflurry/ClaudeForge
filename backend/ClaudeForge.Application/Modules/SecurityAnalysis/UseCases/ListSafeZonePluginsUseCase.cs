using ClaudeForge.Core.Modules.Organizations.Ports;

namespace ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;

/// <summary>
/// Lists all approved safe zone plugins for an organization,
/// including globally-approved plugins (minus org-level blocks).
/// </summary>
public sealed class ListSafeZoneAddOnsUseCase
{
    private readonly ISafeZoneStorePort _safeZoneStore;

    public ListSafeZoneAddOnsUseCase(ISafeZoneStorePort safeZoneStore)
    {
        _safeZoneStore = safeZoneStore;
    }

    /// <summary>
    /// Returns org-specific + global safe zone plugins, minus any blocked globals.
    /// </summary>
    public async Task<IReadOnlyList<SafeZonePluginDetailDto>> ExecuteAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        // Get org-specific safe zone plugins
        IReadOnlyList<SafeZonePluginDetailDto> orgPlugins = await _safeZoneStore
            .ListSafeZonePluginsAsync(orgId, ct);

        // Get globally-approved plugins
        IReadOnlyList<SafeZonePluginDetailDto> globalPlugins = await _safeZoneStore
            .ListGlobalSafeZonePluginsAsync(ct);

        // Get org-level blocked global plugin IDs
        IReadOnlyList<Guid> blockedIds = await _safeZoneStore
            .ListBlockedGlobalAddOnsAsync(orgId, ct);

        // Merge: org-specific + globals not blocked by org
        HashSet<Guid> orgPluginIds = new(orgPlugins.Select(p => p.PluginId));

        List<SafeZonePluginDetailDto> merged = new(orgPlugins);

        foreach (SafeZonePluginDetailDto global in globalPlugins)
        {
            if (!orgPluginIds.Contains(global.PluginId) && !blockedIds.Contains(global.PluginId))
            {
                merged.Add(global with { Label = "GLOBAL" });
            }
        }

        return merged;
    }
}
