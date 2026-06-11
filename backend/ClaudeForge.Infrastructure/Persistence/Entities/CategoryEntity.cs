namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>categories</c> controlled-vocabulary table.
/// PK is a <c>smallserial</c> (short / smallint).
/// UNIQUE(dimension, value) prevents duplicates within a dimension.
/// </summary>
public sealed class CategoryEntity
{
    public short Id { get; set; }

    /// <summary>
    /// Categorisation dimension: <c>'type'</c>, <c>'language'</c>, or <c>'use_case'</c>.
    /// </summary>
    public string Dimension { get; set; } = string.Empty;

    /// <summary>
    /// Controlled-vocabulary value within the dimension
    /// (e.g. <c>'skill'</c>, <c>'typescript'</c>, <c>'dev-team'</c>).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string? Description { get; set; }

    // Navigation property
    public ICollection<AddOnCategoryEntity> PluginCategories { get; set; } = [];
}
