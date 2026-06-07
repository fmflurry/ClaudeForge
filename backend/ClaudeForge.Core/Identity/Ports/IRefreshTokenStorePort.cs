namespace ClaudeForge.Core.Identity.Ports;

/// <summary>
/// Port for storing, rotating, and revoking opaque refresh tokens.
/// Lives in Core.Identity.Ports so that use-cases in Core can depend on it
/// without any reference to Infrastructure types.
/// </summary>
public interface IRefreshTokenStorePort
{
    /// <summary>
    /// Creates a new refresh token. Returns the plain token once — caller must store it.
    /// Persists only SHA-256(plainToken) in token_hash — never the plain token.
    /// Sets RootId = new row's own Id (family root) and persists Provider.
    /// </summary>
    Task<RefreshTokenResult> CreateAsync(
        CreateRefreshTokenCommand cmd,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up a token by SHA-256(plainToken). Returns null when not found.
    /// Empty/whitespace input returns null without a DB hit.
    /// Returns a <see cref="RefreshTokenInfo"/> Core DTO — never an Infrastructure entity.
    /// </summary>
    Task<RefreshTokenInfo?> FindByHashAsync(
        string plainToken,
        CancellationToken ct = default);

    /// <summary>
    /// Atomic one-time-use rotation: conditionally updates the old row
    /// (rotated_to IS NULL AND revoked_at IS NULL) and creates a new token row
    /// that inherits the parent's RootId.
    /// When 0 rows are affected (race/reuse) throws <see cref="System.Security.Authentication.AuthenticationException"/>.
    /// </summary>
    Task<RotateRefreshTokenResult> RotateAsync(
        Guid oldId,
        Guid userId,
        Guid rootId,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes the ENTIRE family in one statement:
    /// UPDATE refresh_tokens SET revoked_at=now() WHERE root_id=@root.
    /// </summary>
    Task RevokeChainAsync(Guid rootId, CancellationToken ct = default);
}
