namespace ClaudeForge.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core entity for the <c>user_identities</c> table.
/// Links an external OIDC identity (provider + subject) to an internal user.
/// UNIQUE(Provider, Subject) is enforced at the database level.
/// </summary>
public sealed class UserIdentityEntity
{
    public Guid Id { get; set; }

    /// <summary>FK → users ON DELETE CASCADE.</summary>
    public Guid UserId { get; set; }

    /// <summary>OIDC provider name, e.g. "google" or "microsoft".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider-issued subject identifier (stable user ID at the IdP).</summary>
    public string Subject { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation property
    public UserEntity User { get; set; } = null!;
}
