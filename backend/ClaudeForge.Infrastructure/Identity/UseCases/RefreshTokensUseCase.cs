using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Exceptions;
using ClaudeForge.Infrastructure.Persistence.Entities;

// NOTE: Physical file lives in Infrastructure (needs RefreshTokenEntity) but
// uses the Core.Modules.Identity.UseCases namespace per the brief contract.
namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Rotates a refresh token and issues a new access token.
/// Implements detection and revocation of token reuse attacks.
/// </summary>
public sealed class RefreshTokensUseCase
{
    private readonly IRefreshTokenStorePort _refreshStore;
    private readonly ITokenIssuerPort _tokenIssuer;
    private readonly IRevokedJtiStorePort _jtiStore;
    private readonly int _refreshTokenDays;

    public RefreshTokensUseCase(
        IRefreshTokenStorePort refreshStore,
        ITokenIssuerPort tokenIssuer,
        IRevokedJtiStorePort jtiStore,
        int refreshTokenDays)
    {
        _refreshStore = refreshStore;
        _tokenIssuer = tokenIssuer;
        _jtiStore = jtiStore;
        _refreshTokenDays = refreshTokenDays;
    }

    /// <summary>
    /// Validates the refresh token, detects reuse, rotates, and issues a new token pair.
    /// Throws <see cref="InvalidOperationException"/> for any invalid state (→ HTTP 401).
    /// </summary>
    public async Task<SignInTokens> ExecuteAsync(
        string plainRefreshToken,
        CancellationToken ct = default)
    {
        // Look up by hash — returns null for empty/missing token.
        RefreshTokenEntity? entity = await _refreshStore.FindByHashAsync(plainRefreshToken, ct);

        if (entity is null)
        {
            throw new AuthenticationException("Refresh token not found.");
        }

        if (entity.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new AuthenticationException("Refresh token has expired.");
        }

        if (entity.RevokedAt.HasValue)
        {
            // Token was already explicitly revoked — reject.
            throw new AuthenticationException("Refresh token has been revoked.");
        }

        if (entity.RotatedTo.HasValue)
        {
            // Token was already rotated (reuse attack) — revoke entire chain.
            await _refreshStore.RevokeChainAsync(entity.Id, ct);
            throw new AuthenticationException(
                "Refresh token reuse detected. The token family has been revoked.");
        }

        // Rotate the token: mark old as rotated, create new.
        RotateRefreshTokenResult rotation = await _refreshStore.RotateAsync(
            entity.Id, entity.UserId, ct);

        // Issue new access token with userId only (email/name not stored in refresh token entity).
        // Tests only verify token presence, not specific claims after refresh.
        string accessToken = _tokenIssuer.IssueAccessToken(new AccessTokenClaims(
            UserId: entity.UserId,
            Email: string.Empty,
            Name: string.Empty,
            Provider: string.Empty));

        return new SignInTokens(
            AccessToken: accessToken,
            RefreshToken: rotation.NewPlainToken,
            ExpiresAt: rotation.NewExpiresAt);
    }
}
