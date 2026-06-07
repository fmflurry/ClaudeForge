using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence.Entities;

// NOTE: This interface lives in the Infrastructure assembly but is declared under
// ClaudeForge.Core.Identity.Ports namespace so that callers (test project) can
// resolve it alongside the other port interfaces without introducing a circular
// Core → Infrastructure project reference (which would be needed if RefreshTokenEntity
// were referenced from the Core assembly).
namespace ClaudeForge.Core.Identity.Ports;

/// <summary>
/// Port for storing, rotating, and revoking opaque refresh tokens.
/// Defined in Infrastructure to avoid a circular project dependency: the return
/// type of <see cref="FindByHashAsync"/> is <see cref="RefreshTokenEntity"/>,
/// which lives in Infrastructure.Persistence.Entities.
/// </summary>
public interface IRefreshTokenStorePort
{
    /// <summary>
    /// Creates a new refresh token. Returns the plain token once — caller must store it.
    /// Persists only SHA-256(plainToken) in token_hash — never the plain token.
    /// </summary>
    Task<RefreshTokenResult> CreateAsync(
        CreateRefreshTokenCommand cmd,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up a token by SHA-256(plainToken). Returns null when not found.
    /// Empty/whitespace input returns null without a DB hit.
    /// </summary>
    Task<RefreshTokenEntity?> FindByHashAsync(
        string plainToken,
        CancellationToken ct = default);

    /// <summary>
    /// One-time-use rotation: marks oldId as rotated_to=newId, creates a new token row.
    /// </summary>
    Task<RotateRefreshTokenResult> RotateAsync(
        Guid oldId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes the full chain rooted at rootId: walks rotated_to links,
    /// sets revoked_at on every node.
    /// </summary>
    Task RevokeChainAsync(Guid rootId, CancellationToken ct = default);
}
