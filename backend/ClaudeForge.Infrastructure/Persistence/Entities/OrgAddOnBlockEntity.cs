namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>org_plugin_blocks</c> table.
/// Tracks globally-approved plugins that a specific org has chosen to block.
/// </summary>
public sealed class OrgAddOnBlockEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → organizations ON DELETE CASCADE.</summary>
    public Guid OrgId { get; set; }

    /// <summary>FK → plugins ON DELETE CASCADE.</summary>
    public Guid PluginId { get; set; }

    /// <summary>User who blocked this plugin for the org.</summary>
    public Guid BlockedBy { get; set; }

    /// <summary>When the block was placed.</summary>
    public DateTimeOffset BlockedAt { get; set; }

    // Navigation properties
    public OrganizationEntity Organization { get; set; } = null!;
    public AddOnEntity AddOn { get; set; } = null!;
}
