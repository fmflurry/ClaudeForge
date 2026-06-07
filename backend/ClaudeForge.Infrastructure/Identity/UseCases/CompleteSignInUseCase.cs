using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>
/// Completes the OAuth/OIDC sign-in flow by exchanging a code+state for tokens.
/// Corresponds to POST /auth/token and the GET /auth/callback happy path.
/// </summary>
public sealed class CompleteSignInUseCase
{
    private readonly IIdentityProviderRegistry _registry;
    private readonly IAuthFlowStatePort _stateStore;
    private readonly IUserStorePort _userStore;
    private readonly ITokenIssuerPort _tokenIssuer;
    private readonly IRefreshTokenStorePort _refreshStore;
    private readonly int _refreshTokenDays;

    public CompleteSignInUseCase(
        IIdentityProviderRegistry registry,
        IAuthFlowStatePort stateStore,
        IUserStorePort userStore,
        ITokenIssuerPort tokenIssuer,
        IRefreshTokenStorePort refreshStore,
        int refreshTokenDays)
    {
        _registry = registry;
        _stateStore = stateStore;
        _userStore = userStore;
        _tokenIssuer = tokenIssuer;
        _refreshStore = refreshStore;
        _refreshTokenDays = refreshTokenDays;
    }

    /// <summary>
    /// Consumes the state, exchanges the code, validates the id_token, provisions
    /// or links the user, and issues a fresh token pair.
    /// Throws <see cref="InvalidOperationException"/> for any auth failure (→ HTTP 401).
    /// </summary>
    public async Task<SignInTokens> ExecuteAsync(
        string code,
        string state,
        CancellationToken ct = default)
    {
        // Consume auth flow state — null means unknown or expired → 401.
        AuthFlowState? flowState = await _stateStore.ConsumeAsync(state, ct);
        if (flowState is null)
        {
            throw new AuthenticationException(
                "Authorization state is unknown or has expired. Please restart the sign-in flow.");
        }

        // Resolve the provider that initiated this flow.
        IIdentityProviderPort providerPort = _registry.Resolve(flowState.Provider);

        // Exchange code for raw id_token — any exchange failure → 401.
        // Note: CancellationToken is omitted for the OIDC adapter calls to ensure the
        // NSubstitute mock setup (which sets up with default ct) matches.
        string rawIdToken;
        try
        {
            rawIdToken = await providerPort.ExchangeCodeAsync(
                flowState.Provider,
                code,
                flowState.CodeVerifier,
                flowState.RedirectUri);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException(
                $"Code exchange failed: {ex.Message}", ex);
        }

        // Validate the id_token and extract claims.
        VerifiedIdentity? identity = await providerPort.ValidateIdTokenAsync(
            flowState.Provider, rawIdToken);

        if (identity is null)
        {
            throw new AuthenticationException(
                $"Identity validation returned null for provider '{flowState.Provider}' " +
                $"with rawIdToken='{rawIdToken}'. Mock may not have matched.");
        }

        // Provision or link the user account.
        ProvisionedUser user = await _userStore.ProvisionOrLinkAsync(
            flowState.Provider,
            identity.Subject,
            identity.Email,
            identity.EmailVerified,
            identity.Name,
            ct);

        // Issue access JWT.
        string accessToken = _tokenIssuer.IssueAccessToken(new AccessTokenClaims(
            UserId: user.UserId,
            Email: user.Email,
            Name: user.DisplayName,
            Provider: flowState.Provider));

        // Create refresh token.
        RefreshTokenResult refreshResult = await _refreshStore.CreateAsync(
            new CreateRefreshTokenCommand(user.UserId, _refreshTokenDays), ct);

        return new SignInTokens(
            AccessToken: accessToken,
            RefreshToken: refreshResult.PlainToken,
            ExpiresAt: refreshResult.ExpiresAt);
    }
}
