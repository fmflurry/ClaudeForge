namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>plugins</c> table.
/// Mutable to allow EF change tracking; immutability lives at the domain layer.
/// </summary>
public sealed class PluginEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Lower-cased name for case-insensitive duplicate detection (UNIQUE constraint).
    /// Must equal <c>Name.ToLowerInvariant()</c>.
    /// </summary>
    public string NameNormalized { get; set; } = string.Empty;

    /// <summary>
    /// URL-friendly slug (UNIQUE constraint).
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public long DownloadCount { get; set; }

    /// <summary>
    /// PostgreSQL <c>tsvector</c> column populated by a DB trigger.
    /// Mapped as <c>string?</c> because EF Core has no first-class tsvector type.
    /// </summary>
    public string? SearchVector { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<PluginVersionEntity> Versions { get; set; } = [];
    public ICollection<PluginCategoryEntity> PluginCategories { get; set; } = [];
}
