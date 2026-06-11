namespace ClaudeForge.Core.Modules.Organizations.Ports;

/// <summary>
/// Port for safe zone plugin operations.
/// Implementations persist safe zone entries and query them with plugin metadata.
/// </summary>
public interface ISafeZoneStorePort
{
    /// <summary>
    /// Checks whether a plugin has security_status == "passed" and is eligible for safe zone.
    /// Returns (true, null) when eligible, (false, reason) when not.
    /// </summary>
    Task<(bool Eligible, string? Reason)> IsAddOnEligibleAsync(
        Guid pluginId,
        CancellationToken ct = default);

    /// <summary>Approves a plugin for the org's safe zone. Returns the created entry DTO.</summary>
    Task<SafeZoneEntryDto?> ApproveAddOnAsync(
        Guid orgId,
        Guid pluginId,
        string pluginVersion,
        Guid approvedBy,
        CancellationToken ct = default);

    /// <summary>Returns all approved (active) safe zone plugins for an org, including plugin details.</summary>
    Task<IReadOnlyList<SafeZonePluginDetailDto>> ListSafeZonePluginsAsync(
        Guid orgId,
        CancellationToken ct = default);

    /// <summary>Returns plugins that passed security analysis but are NOT yet approved for the org's safe zone.</summary>
    Task<IReadOnlyList<PendingSafeZonePluginDto>> ListPendingAddOnsAsync(
        Guid orgId,
        CancellationToken ct = default);

    /// <summary>Returns a specific safe zone entry, or null if not found.</summary>
    Task<SafeZoneEntryDto?> FindEntryAsync(
        Guid orgId,
        Guid pluginId,
        string? pluginVersion = null,
        CancellationToken ct = default);

    // ── Global safe zone (3.3.4) ───────────────────────────────────────────

    /// <summary>Approves a plugin globally (visible to all orgs). Returns the created entry DTO.</summary>
    Task<SafeZoneEntryDto?> ApproveAddOnGlobalAsync(
        Guid pluginId,
        string pluginVersion,
        Guid approvedBy,
        CancellationToken ct = default);

    /// <summary>Returns all globally-approved safe zone plugins.</summary>
    Task<IReadOnlyList<SafeZonePluginDetailDto>> ListGlobalSafeZonePluginsAsync(
        CancellationToken ct = default);

    // ── Org-level blocks for global plugins (3.3.5) ────────────────────────

    /// <summary>Blocks a globally-approved plugin for a specific org.</summary>
    Task BlockGlobalAddOnAsync(
        Guid orgId,
        Guid pluginId,
        Guid blockedBy,
        CancellationToken ct = default);

    /// <summary>Unblocks a globally-approved plugin for a specific org.</summary>
    Task UnblockGlobalAddOnAsync(
        Guid orgId,
        Guid pluginId,
        CancellationToken ct = default);

    /// <summary>Returns true if the org has blocked the globally-approved plugin.</summary>
    Task<bool> IsGloballyBlockedAsync(
        Guid orgId,
        Guid pluginId,
        CancellationToken ct = default);

    /// <summary>Returns all globally-blocked plugin IDs for the org.</summary>
    Task<IReadOnlyList<Guid>> ListBlockedGlobalAddOnsAsync(
        Guid orgId,
        CancellationToken ct = default);
}

/// <summary>DTO for a safe zone entry (used internally).</summary>
public sealed record SafeZoneEntryDto(
    Guid Id,
    Guid OrgId,
    Guid PluginId,
    string PluginVersion,
    Guid ApprovedBy,
    DateTimeOffset ApprovedAt,
    bool IsActive);

/// <summary>DTO for a safe zone plugin with details, returned to clients.</summary>
public sealed record SafeZonePluginDetailDto(
    Guid Id,
    Guid PluginId,
    string Name,
    string Slug,
    string PluginVersion,
    decimal SecurityScore,
    string SecurityStatus,
    Guid ApprovedBy,
    DateTimeOffset ApprovedAt,
    string Label);

/// <summary>DTO for a plugin that passed analysis but isn't yet in the safe zone.</summary>
public sealed record PendingSafeZonePluginDto(
    Guid PluginId,
    string Name,
    string Slug,
    decimal SecurityScore,
    string SecurityStatus);
