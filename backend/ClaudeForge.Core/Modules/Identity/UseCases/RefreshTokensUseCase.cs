using ClaudeForge.Core.Identity.Ports;
using AuthEx = ClaudeForge.Core.Shared.Exceptions.AuthenticationException;
using SysAuthEx = System.Security.Authentication.AuthenticationException;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Rotates a refresh token and issues a new access token.
/// Implements detection and revocation of token reuse attacks.
/// </summary>
public sealed class RefreshTokensUseCase
{
    private readonly IRefreshTokenStorePort _refreshStore;
    private readonly ITokenIssuerPort _tokenIssuer;
    private readonly IUserStorePort _userStore;
    private readonly IRevokedJtiStorePort _jtiStore;
    private readonly int _refreshTokenDays;

    public RefreshTokensUseCase(
        IRefreshTokenStorePort refreshStore,
        ITokenIssuerPort tokenIssuer,
        IUserStorePort userStore,
        IRevokedJtiStorePort jtiStore,
        int refreshTokenDays)
    {
        _refreshStore = refreshStore;
        _tokenIssuer = tokenIssuer;
        _userStore = userStore;
        _jtiStore = jtiStore;
        _refreshTokenDays = refreshTokenDays;
    }

    /// <summary>
    /// Validates the refresh token, detects reuse, rotates, and issues a new token pair.
    /// Throws <see cref="AuthEx"/> for any invalid state (→ HTTP 401).
    /// </summary>
    public async Task<SignInTokens> ExecuteAsync(
        string plainRefreshToken,
        CancellationToken ct = default)
    {
        // Look up by hash — returns null for empty/missing token.
        RefreshTokenInfo? tokenInfo = await _refreshStore.FindByHashAsync(plainRefreshToken, ct);

        if (tokenInfo is null)
        {
            throw new AuthEx("Refresh token not found.");
        }

        if (tokenInfo.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new AuthEx("Refresh token has expired.");
        }

        if (tokenInfo.RevokedAt.HasValue)
        {
            // Token was already explicitly revoked — reject.
            throw new AuthEx("Refresh token has been revoked.");
        }

        if (tokenInfo.RotatedTo.HasValue)
        {
            // Token was already rotated (reuse attack) — revoke entire family by RootId.
            await _refreshStore.RevokeChainAsync(tokenInfo.RootId, ct);
            throw new AuthEx(
                "Refresh token reuse detected. The token family has been revoked.");
        }

        // Rotate the token atomically. If RotateAsync throws AuthenticationException,
        // it means a concurrent rotation already fired — propagate 401.
        RotateRefreshTokenResult rotation;
        try
        {
            rotation = await _refreshStore.RotateAsync(
                tokenInfo.Id, tokenInfo.UserId, tokenInfo.RootId, ct);
        }
        catch (SysAuthEx)
        {
            // Concurrent rotation race detected — revoke family and reject.
            await _refreshStore.RevokeChainAsync(tokenInfo.RootId, ct);
            throw new AuthEx(
                "Refresh token reuse detected. The token family has been revoked.");
        }

        // Load the user to issue a token with real claims (HIGH-1).
        UserProfile? profile = await _userStore.FindByIdAsync(tokenInfo.UserId, ct);
        if (profile is null)
        {
            throw new AuthEx($"User {tokenInfo.UserId} not found.");
        }

        // Issue new access token with real Email/DisplayName and the persisted Provider.
        string accessToken = _tokenIssuer.IssueAccessToken(new AccessTokenClaims(
            UserId: tokenInfo.UserId,
            Email: profile.Email,
            Name: profile.DisplayName,
            Provider: tokenInfo.Provider));

        return new SignInTokens(
            AccessToken: accessToken,
            RefreshToken: rotation.NewPlainToken,
            ExpiresAt: rotation.NewExpiresAt);
    }
}
