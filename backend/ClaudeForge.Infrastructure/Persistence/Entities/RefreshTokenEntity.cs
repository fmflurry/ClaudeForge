namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>refresh_tokens</c> table.
/// Stores hashed one-time-use refresh tokens with rotation support.
/// </summary>
public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → users ON DELETE CASCADE.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hex digest of the opaque token (CHAR(64), UNIQUE).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Self-FK → refresh_tokens. Points to the successor token after rotation.
    /// Null means this token has not been rotated yet.
    /// </summary>
    public Guid? RotatedTo { get; set; }

    /// <summary>
    /// Root of the token family (set to this row's own Id at creation;
    /// inherited from parent at rotation). Used for atomic family revocation.
    /// </summary>
    public Guid RootId { get; set; }

    /// <summary>
    /// OIDC provider name used during sign-in (e.g. "google", "microsoft").
    /// Persisted so that the refresh path can re-issue claims with the correct provider.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    public UserEntity User { get; set; } = null!;

    /// <summary>Navigation to the successor token (self-FK).</summary>
    public RefreshTokenEntity? RotatedToToken { get; set; }
}
