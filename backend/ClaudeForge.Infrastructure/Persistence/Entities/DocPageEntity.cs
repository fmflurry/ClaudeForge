namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>doc_pages</c> table.
/// Represents static markdown documentation pages.
/// <c>search_vector</c> is a PostgreSQL GENERATED ALWAYS AS STORED tsvector column;
/// EF never writes it — the DB computes it automatically on every INSERT/UPDATE.
/// </summary>
public sealed class DocPageEntity
{
    public Guid Id { get; set; }

    /// <summary>URL-friendly slug (UNIQUE constraint).</summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// PostgreSQL <c>tsvector</c> GENERATED ALWAYS AS STORED column.
    /// Weighted: title=A (higher), content_markdown=B (lower).
    /// Mapped as <c>string?</c> — the DB computes it; EF never writes it.
    /// </summary>
    public string? SearchVector { get; set; }
}
