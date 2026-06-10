namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>safe_zone_plugins</c> table.
/// Tracks which plugins have been approved for which org's safe zone.
/// Enforces a UNIQUE constraint on (org_id, plugin_id, plugin_version).
/// </summary>
public sealed class SafeZonePluginEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → organizations ON DELETE CASCADE.</summary>
    public Guid OrgId { get; set; }

    /// <summary>FK → plugins ON DELETE CASCADE.</summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// Specific plugin version approved for the safe zone.
    /// Combined with OrgId and PluginId in a UNIQUE constraint.
    /// </summary>
    public string PluginVersion { get; set; } = string.Empty;

    /// <summary>User who approved this plugin for the safe zone.</summary>
    public Guid ApprovedBy { get; set; }

    /// <summary>When the approval was granted.</summary>
    public DateTimeOffset ApprovedAt { get; set; }

    /// <summary>Whether this safe zone entry is currently active.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public OrganizationEntity Organization { get; set; } = null!;
    public PluginEntity Plugin { get; set; } = null!;
}
