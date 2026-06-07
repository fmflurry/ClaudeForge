namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>revoked_jti</c> table.
/// Stores JWT identifiers that have been explicitly revoked before their natural expiry.
/// The <see cref="ExpiresAt"/> column mirrors the original token's expiry so expired
/// entries can be cleaned up by a background job.
/// </summary>
public sealed class RevokedJtiEntity
{
    /// <summary>The JWT "jti" claim value. Primary key (TEXT).</summary>
    public string Jti { get; set; } = string.Empty;

    /// <summary>Timestamp after which this denylist entry is considered expired. TIMESTAMPTZ NOT NULL.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
