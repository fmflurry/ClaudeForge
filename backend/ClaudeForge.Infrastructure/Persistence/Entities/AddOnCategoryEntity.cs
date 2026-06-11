namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>plugin_categories</c> join table.
/// Composite PK: (plugin_id, category_id).
/// </summary>
public sealed class AddOnCategoryEntity
{
    public Guid PluginId { get; set; }
    public short CategoryId { get; set; }

    // Navigation properties
    public AddOnEntity AddOn { get; set; } = null!;
    public CategoryEntity Category { get; set; } = null!;
}
