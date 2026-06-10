namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>author_reputation</c> table.
/// Tracks author karma, level, and earned badges.
/// PK is <c>author_id</c> (no surrogate — maps 1:1 with a user GUID).
/// </summary>
public sealed class AuthorReputationEntity
{
    /// <summary>
    /// Author identifier (maps to a user GUID in the application layer).
    /// Serves as both the logical and physical PK — no surrogate column.
    /// </summary>
    public Guid AuthorId { get; set; }

    public int KarmaPoints { get; set; }

    public int Level { get; set; } = 1;

    /// <summary>JSONB array of badge names/identifiers.</summary>
    public string Badges { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
