using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Approves a pending device authorization request (RFC 8628 §3.3).
///
/// An authenticated browser user submits the user_code displayed by the device.
/// The use case validates the code, mints access and refresh tokens on behalf of
/// the approving user, and persists them in the <see cref="DeviceCodeStore"/> so
/// that the polling device can retrieve them via <see cref="PollDeviceTokenUseCase"/>.
/// </summary>
public sealed class ApproveDeviceCodeUseCase
{
    private readonly DeviceCodeStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly ITokenIssuerPort _tokenIssuer;
    private readonly IRefreshTokenStorePort _refreshStore;
    private readonly IUserStorePort _userStore;
    private readonly int _refreshTokenDays;

    public ApproveDeviceCodeUseCase(
        DeviceCodeStore store,
        ICurrentUser currentUser,
        ITokenIssuerPort tokenIssuer,
        IRefreshTokenStorePort refreshStore,
        IUserStorePort userStore,
        int refreshTokenDays)
    {
        _store = store;
        _currentUser = currentUser;
        _tokenIssuer = tokenIssuer;
        _refreshStore = refreshStore;
        _userStore = userStore;
        _refreshTokenDays = refreshTokenDays;
    }

    /// <summary>
    /// Validates the user code and, if valid, mints tokens for the approving user.
    /// </summary>
    public async Task<ApproveDeviceCodeResult> ExecuteAsync(
        string? userCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userCode))
        {
            return new ApproveDeviceCodeResult.NotFound();
        }

        DeviceAuthState? state = _store.FindByUserCode(userCode);
        if (state is null)
        {
            return new ApproveDeviceCodeResult.NotFound();
        }

        if (state.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _store.Remove(state.DeviceCode);
            return new ApproveDeviceCodeResult.Expired();
        }

        if (state.Tokens is not null)
        {
            return new ApproveDeviceCodeResult.AlreadyApproved();
        }

        Guid userId = _currentUser.UserId!.Value;

        UserProfile? profile = await _userStore.FindByIdAsync(userId, ct);

        string accessToken = _tokenIssuer.IssueAccessToken(
            new AccessTokenClaims(
                UserId: userId,
                Email: profile?.Email ?? _currentUser.Email ?? string.Empty,
                Name: profile?.DisplayName ?? string.Empty,
                Provider: state.Provider));

        RefreshTokenResult refresh = await _refreshStore.CreateAsync(
            new CreateRefreshTokenCommand(
                UserId: userId,
                ExpiryDays: _refreshTokenDays,
                Provider: state.Provider),
            ct);

        _store.Update(state with
        {
            Tokens = new SignInTokens(
                AccessToken: accessToken,
                RefreshToken: refresh.PlainToken,
                ExpiresAt: refresh.ExpiresAt)
        });

        return new ApproveDeviceCodeResult.Success();
    }
}
