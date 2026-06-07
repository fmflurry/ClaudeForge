using ClaudeForge.Core.Identity.Ports;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Signs the user out by revoking their refresh token chain and optionally
/// denylisting the current access token's jti.
/// </summary>
public sealed class SignOutUseCase
{
    private readonly IRefreshTokenStorePort _refreshStore;
    private readonly IRevokedJtiStorePort _jtiStore;

    public SignOutUseCase(
        IRefreshTokenStorePort refreshStore,
        IRevokedJtiStorePort jtiStore)
    {
        _refreshStore = refreshStore;
        _jtiStore = jtiStore;
    }

    /// <summary>
    /// Revokes the refresh token chain and optionally adds the access token jti to the denylist.
    /// Silent success even if the refresh token is already revoked or not found.
    /// </summary>
    public async Task ExecuteAsync(
        string plainRefreshToken,
        string? accessJti,
        DateTimeOffset? accessExpiresAt,
        CancellationToken ct = default)
    {
        // Revoke the refresh token chain using the family RootId.
        RefreshTokenInfo? tokenInfo = await _refreshStore.FindByHashAsync(plainRefreshToken, ct);
        if (tokenInfo is not null)
        {
            await _refreshStore.RevokeChainAsync(tokenInfo.RootId, ct);
        }

        // Optionally denylist the access token jti.
        if (!string.IsNullOrWhiteSpace(accessJti) && accessExpiresAt.HasValue)
        {
            await _jtiStore.AddAsync(accessJti, accessExpiresAt.Value, ct);
        }
    }
}
