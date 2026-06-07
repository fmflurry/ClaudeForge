using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Modules.Identity.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for <see cref="ApproveDeviceCodeUseCase"/> — the use case that an
/// authenticated browser user invokes to grant a pending device authorization request
/// (RFC 8628 §3.3).
///
/// RED: <see cref="ApproveDeviceCodeUseCase"/> does not exist yet — all tests in this
/// class must fail to compile / throw until the GREEN implementation is shipped.
/// </summary>
public sealed class ApproveDeviceCodeUseCaseTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ICurrentUser MakeAuthenticatedUser(Guid userId, string email = "approver@test.example.com")
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Email.Returns(email);
        return user;
    }

    private static ITokenIssuerPort MakeTokenIssuer(string accessToken = "minted-access-token")
    {
        ITokenIssuerPort port = Substitute.For<ITokenIssuerPort>();
        port.IssueAccessToken(Arg.Any<AccessTokenClaims>()).Returns(accessToken);
        return port;
    }

    private static IRefreshTokenStorePort MakeRefreshStore(string plainToken = "minted-refresh-token")
    {
        IRefreshTokenStorePort port = Substitute.For<IRefreshTokenStorePort>();
        port.CreateAsync(Arg.Any<CreateRefreshTokenCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RefreshTokenResult(
                Id: Guid.NewGuid(),
                UserId: Guid.NewGuid(),
                PlainToken: plainToken,
                ExpiresAt: DateTimeOffset.UtcNow.AddDays(30))));
        return port;
    }

    private static IUserStorePort MakeUserStore(Guid userId, string email = "approver@test.example.com", string displayName = "Approver")
    {
        IUserStorePort port = Substitute.For<IUserStorePort>();
        port.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserProfile?>(new UserProfile(
                UserId: userId,
                Email: email,
                DisplayName: displayName,
                OrgMemberships: Array.Empty<UserOrgMembership>())));
        return port;
    }

    private static DeviceAuthState MakePendingState(
        string deviceCode = "dc-test",
        string userCode = "USRCODE1",
        bool expired = false)
    {
        DateTimeOffset expiresAt = expired
            ? DateTimeOffset.UtcNow.AddSeconds(-1)
            : DateTimeOffset.UtcNow.AddMinutes(15);

        return new DeviceAuthState(
            DeviceCode: deviceCode,
            UserCode: userCode,
            Provider: "google",
            ExpiresAt: expiresAt);
    }

    private static ApproveDeviceCodeUseCase MakeUseCase(
        DeviceCodeStore store,
        ICurrentUser? currentUser = null,
        ITokenIssuerPort? tokenIssuer = null,
        IRefreshTokenStorePort? refreshStore = null,
        IUserStorePort? userStore = null,
        int refreshTokenDays = 30)
    {
        Guid userId = currentUser?.UserId ?? Guid.NewGuid();
        return new ApproveDeviceCodeUseCase(
            store,
            currentUser ?? MakeAuthenticatedUser(userId),
            tokenIssuer ?? MakeTokenIssuer(),
            refreshStore ?? MakeRefreshStore(),
            userStore ?? MakeUserStore(userId),
            refreshTokenDays);
    }

    // -----------------------------------------------------------------------
    // Happy path: valid pending user_code + authenticated user
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ValidPendingUserCode_ReturnsSuccess()
    {
        DeviceCodeStore store = new();
        DeviceAuthState state = MakePendingState(deviceCode: "dc-happy", userCode: "HAPPY001");
        store.Store(state);

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        ApproveDeviceCodeResult result = await useCase.ExecuteAsync("HAPPY001");

        Assert.IsType<ApproveDeviceCodeResult.Success>(result);
    }

    [Fact]
    public async Task ExecuteAsync_ValidPendingUserCode_PersistsTokensInStore()
    {
        DeviceCodeStore store = new();
        DeviceAuthState state = MakePendingState(deviceCode: "dc-persist", userCode: "PERSIST1");
        store.Store(state);

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store,
            tokenIssuer: MakeTokenIssuer("stored-access"),
            refreshStore: MakeRefreshStore("stored-refresh"));

        await useCase.ExecuteAsync("PERSIST1");

        DeviceAuthState? updated = store.FindByDeviceCode("dc-persist");
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Tokens);
        Assert.Equal("stored-access", updated.Tokens!.AccessToken);
        Assert.Equal("stored-refresh", updated.Tokens.RefreshToken);
    }

    [Fact]
    public async Task ExecuteAsync_ValidPendingUserCode_TokensContainApprovingUserSub()
    {
        Guid approverId = Guid.NewGuid();
        ICurrentUser approver = MakeAuthenticatedUser(approverId, "approver@test.example.com");
        ITokenIssuerPort tokenIssuer = Substitute.For<ITokenIssuerPort>();
        tokenIssuer.IssueAccessToken(Arg.Any<AccessTokenClaims>()).Returns("ok-token");

        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-sub", userCode: "SUBCODE1"));

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store,
            currentUser: approver,
            tokenIssuer: tokenIssuer,
            userStore: MakeUserStore(approverId));

        await useCase.ExecuteAsync("SUBCODE1");

        // The token issuer must have been called with the approving user's claims.
        tokenIssuer.Received(1).IssueAccessToken(
            Arg.Is<AccessTokenClaims>(c => c.UserId == approverId));
    }

    [Fact]
    public async Task ExecuteAsync_ValidPendingUserCode_IssuesAccessAndRefreshTokens()
    {
        Guid approverId = Guid.NewGuid();
        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-tokens", userCode: "TOKCODE1"));

        IRefreshTokenStorePort refreshStore = MakeRefreshStore("rf-plain");
        ApproveDeviceCodeUseCase useCase = MakeUseCase(store,
            currentUser: MakeAuthenticatedUser(approverId),
            tokenIssuer: MakeTokenIssuer("at-value"),
            refreshStore: refreshStore,
            userStore: MakeUserStore(approverId));

        await useCase.ExecuteAsync("TOKCODE1");

        // Refresh token must have been created for the approving user.
        await refreshStore.Received(1).CreateAsync(
            Arg.Is<CreateRefreshTokenCommand>(c => c.UserId == approverId),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Unknown user_code → NotFound
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_UnknownUserCode_ReturnsNotFound()
    {
        DeviceCodeStore store = new();
        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        ApproveDeviceCodeResult result = await useCase.ExecuteAsync("UNKNOWN1");

        Assert.IsType<ApproveDeviceCodeResult.NotFound>(result);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUserCode_ReturnsNotFound()
    {
        DeviceCodeStore store = new();
        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        ApproveDeviceCodeResult result = await useCase.ExecuteAsync(string.Empty);

        Assert.IsType<ApproveDeviceCodeResult.NotFound>(result);
    }

    // -----------------------------------------------------------------------
    // Expired user_code → Expired
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ExpiredUserCode_ReturnsExpired()
    {
        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-exp", userCode: "EXPIRED1", expired: true));

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        ApproveDeviceCodeResult result = await useCase.ExecuteAsync("EXPIRED1");

        Assert.IsType<ApproveDeviceCodeResult.Expired>(result);
    }

    // -----------------------------------------------------------------------
    // Already-approved user_code → AlreadyApproved (conflict / single-use)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_AlreadyApprovedUserCode_ReturnsAlreadyApproved()
    {
        DeviceCodeStore store = new();
        SignInTokens existingTokens = new("at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        DeviceAuthState state = MakePendingState(deviceCode: "dc-already", userCode: "ALREDY01")
            with { Tokens = existingTokens };
        store.Store(state);

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        ApproveDeviceCodeResult result = await useCase.ExecuteAsync("ALREDY01");

        Assert.IsType<ApproveDeviceCodeResult.AlreadyApproved>(result);
    }

    // -----------------------------------------------------------------------
    // Token minting: approving user claims flow
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ValidApproval_FetchesUserProfileForTokenClaims()
    {
        Guid approverId = Guid.NewGuid();
        IUserStorePort userStore = MakeUserStore(approverId, "check@test.example.com", "Check User");
        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-profile", userCode: "PROFCODE"));

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store,
            currentUser: MakeAuthenticatedUser(approverId),
            userStore: userStore);

        await useCase.ExecuteAsync("PROFCODE");

        await userStore.Received(1).FindByIdAsync(approverId, Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Poll-before-approval still returns Pending
    // (Tested via PollDeviceTokenUseCase — no Tokens yet in store)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BeforeApproval_Poll_ReturnsPending()
    {
        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-preapprove", userCode: "PREAPPR1"));

        PollDeviceTokenUseCase pollUseCase = new(store);

        DeviceTokenPollResult result = await pollUseCase.ExecuteAsync("dc-preapprove");

        Assert.IsType<DeviceTokenPollResult.Pending>(result);
    }

    // -----------------------------------------------------------------------
    // Poll-after-approval returns Approved with minted tokens
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AfterApproval_Poll_ReturnsApprovedWithMintedTokens()
    {
        Guid approverId = Guid.NewGuid();
        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-postapprove", userCode: "POSTAP01"));

        ApproveDeviceCodeUseCase approveUseCase = MakeUseCase(store,
            currentUser: MakeAuthenticatedUser(approverId),
            tokenIssuer: MakeTokenIssuer("post-access"),
            refreshStore: MakeRefreshStore("post-refresh"),
            userStore: MakeUserStore(approverId));

        await approveUseCase.ExecuteAsync("POSTAP01");

        // Now poll must return Approved with the tokens minted during approval.
        PollDeviceTokenUseCase pollUseCase = new(store);
        DeviceTokenPollResult result = await pollUseCase.ExecuteAsync("dc-postapprove");

        DeviceTokenPollResult.Approved approved = Assert.IsType<DeviceTokenPollResult.Approved>(result);
        Assert.Equal("post-access", approved.Tokens.AccessToken);
        Assert.Equal("post-refresh", approved.Tokens.RefreshToken);
    }

    // -----------------------------------------------------------------------
    // User_code is case-insensitive lookup (per DeviceCodeStore OrdinalIgnoreCase)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_UserCodeCaseInsensitive_FindsCode()
    {
        DeviceCodeStore store = new();
        store.Store(MakePendingState(deviceCode: "dc-case", userCode: "ABCD1234"));

        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        // Submit in lowercase — should still match
        ApproveDeviceCodeResult result = await useCase.ExecuteAsync("abcd1234");

        Assert.IsType<ApproveDeviceCodeResult.Success>(result);
    }

    // -----------------------------------------------------------------------
    // Null user code
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_NullUserCode_ReturnsNotFound()
    {
        DeviceCodeStore store = new();
        ApproveDeviceCodeUseCase useCase = MakeUseCase(store);

        ApproveDeviceCodeResult result = await useCase.ExecuteAsync(null!);

        Assert.IsType<ApproveDeviceCodeResult.NotFound>(result);
    }
}
