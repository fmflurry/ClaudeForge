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
    /// PostgreSQL <c>tsvector</c> GENERATED ALWAYS AS STORED column.
    /// The DB computes it automatically from <c>name</c> and <c>description</c> on every row write.
    /// Mapped as <c>string?</c> so that callers can use standard string assertions on the lexeme output.
    /// </summary>
    public string? SearchVector { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Plugin visibility: "public" (default) or "private".
    /// Private plugins require a non-NULL <see cref="OwnerOrgId"/>
    /// (enforced by CHECK constraint chk_visibility_owner).
    /// </summary>
    public string Visibility { get; set; } = "public";

    /// <summary>
    /// FK → organizations. Owning organization for private (and optionally public) plugins.
    /// Must be non-NULL when Visibility is "private".
    /// </summary>
    public Guid? OwnerOrgId { get; set; }

    /// <summary>
    /// FK → users. The user within the org who owns / published this plugin.
    /// Nullable.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    // Navigation properties
    public ICollection<PluginVersionEntity> Versions { get; set; } = [];
    public ICollection<PluginCategoryEntity> PluginCategories { get; set; } = [];
}
