using ClaudeForge.Core.Modules.Identity.UseCases;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for <see cref="DeviceCodeStore"/> and <see cref="IssueDeviceCodeUseCase"/>
/// covering expiry/eviction, not-found, and duplicate-code branches, plus
/// <see cref="PollDeviceTokenUseCase"/> state machine transitions.
/// </summary>
public sealed class DeviceCodeStoreTests
{
    // -----------------------------------------------------------------------
    // DeviceCodeStore — Store / FindByDeviceCode
    // -----------------------------------------------------------------------

    [Fact]
    public void Store_ValidState_CanBeFoundByDeviceCode()
    {
        DeviceCodeStore store = new();
        DeviceAuthState state = new(
            DeviceCode: "dc-001",
            UserCode: "ABCDEFGH",
            Provider: "google",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));

        store.Store(state);

        DeviceAuthState? found = store.FindByDeviceCode("dc-001");
        Assert.NotNull(found);
        Assert.Equal("dc-001", found!.DeviceCode);
    }

    [Fact]
    public void FindByDeviceCode_UnknownCode_ReturnsNull()
    {
        DeviceCodeStore store = new();

        DeviceAuthState? found = store.FindByDeviceCode("nonexistent-code");

        Assert.Null(found);
    }

    [Fact]
    public void Remove_ExistingCode_SubsequentFindReturnsNull()
    {
        DeviceCodeStore store = new();
        DeviceAuthState state = new(
            DeviceCode: "dc-remove",
            UserCode: "REMOVE01",
            Provider: "google",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));
        store.Store(state);

        store.Remove("dc-remove");

        Assert.Null(store.FindByDeviceCode("dc-remove"));
    }

    [Fact]
    public void Remove_NonExistentCode_DoesNotThrow()
    {
        DeviceCodeStore store = new();

        // Should be idempotent / no-op
        store.Remove("ghost-code");
    }

    [Fact]
    public void Update_ExistingState_ReplacesRecord()
    {
        DeviceCodeStore store = new();
        SignInTokens tokens = new("access", "refresh", DateTimeOffset.UtcNow.AddHours(1));
        DeviceAuthState initial = new(
            DeviceCode: "dc-upd",
            UserCode: "UPDDCODE",
            Provider: "google",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));
        store.Store(initial);

        DeviceAuthState updated = initial with { Tokens = tokens };
        store.Update(updated);

        DeviceAuthState? found = store.FindByDeviceCode("dc-upd");
        Assert.NotNull(found);
        Assert.NotNull(found!.Tokens);
        Assert.Equal("access", found.Tokens!.AccessToken);
    }

    // -----------------------------------------------------------------------
    // DeviceCodeStore — expiry / sweep
    // -----------------------------------------------------------------------

    [Fact]
    public void Store_ExpiredEntries_AreSweepedOnNextStore()
    {
        DeviceCodeStore store = new();

        // Store an already-expired entry
        DeviceAuthState expired = new(
            DeviceCode: "dc-expired",
            UserCode: "EXPIREDA",
            Provider: "google",
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));
        store.Store(expired);

        // Storing a second entry triggers sweep of expired ones
        DeviceAuthState fresh = new(
            DeviceCode: "dc-fresh",
            UserCode: "FRESHCOD",
            Provider: "google",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));
        store.Store(fresh);

        // The expired entry must have been evicted
        Assert.Null(store.FindByDeviceCode("dc-expired"));
        // The fresh entry must remain
        Assert.NotNull(store.FindByDeviceCode("dc-fresh"));
    }

    // -----------------------------------------------------------------------
    // DeviceCodeStore — RecordPollAndCheckSlowDown
    // -----------------------------------------------------------------------

    [Fact]
    public void RecordPollAndCheckSlowDown_FirstPoll_ReturnsFalse()
    {
        DeviceCodeStore store = new();

        bool tooFast = store.RecordPollAndCheckSlowDown("dc-poll", intervalSeconds: 5);

        Assert.False(tooFast);
    }

    [Fact]
    public void RecordPollAndCheckSlowDown_ImmediateSecondPoll_ReturnsTrue()
    {
        DeviceCodeStore store = new();

        // First poll records timestamp
        store.RecordPollAndCheckSlowDown("dc-poll", intervalSeconds: 5);

        // Immediate second poll within the interval → slow-down
        bool tooFast = store.RecordPollAndCheckSlowDown("dc-poll", intervalSeconds: 5);

        Assert.True(tooFast);
    }

    [Fact]
    public void RecordPollAndCheckSlowDown_UnknownCode_ReturnsFalse()
    {
        DeviceCodeStore store = new();

        // A code that has never been polled is treated as first-time
        bool tooFast = store.RecordPollAndCheckSlowDown("never-polled", intervalSeconds: 5);

        Assert.False(tooFast);
    }
}

/// <summary>
/// Unit tests for <see cref="IssueDeviceCodeUseCase"/>.
/// </summary>
public sealed class IssueDeviceCodeUseCaseTests
{
    private static ClaudeForge.Core.Identity.Ports.IIdentityProviderRegistry MakeRegistry(
        string supportedProvider = "google")
    {
        ClaudeForge.Core.Identity.Ports.IIdentityProviderRegistry registry =
            NSubstitute.Substitute.For<ClaudeForge.Core.Identity.Ports.IIdentityProviderRegistry>();

        ClaudeForge.Core.Identity.Ports.IIdentityProviderPort fakePort =
            NSubstitute.Substitute.For<ClaudeForge.Core.Identity.Ports.IIdentityProviderPort>();

        // Resolve succeeds for the supported provider
        NSubstitute.SubstituteExtensions.Returns(
            registry.Resolve(supportedProvider),
            fakePort);

        // Resolve throws for any other provider
        NSubstitute.SubstituteExtensions.When(
            registry,
            r => r.Resolve(NSubstitute.Arg.Is<string>(p => p != supportedProvider)))
            .Do(_ => throw new ClaudeForge.Core.Identity.Ports.UnsupportedProviderException("bad"));

        return registry;
    }

    [Fact]
    public async Task ExecuteAsync_ValidProvider_ReturnsDeviceCodeResponse()
    {
        DeviceCodeStore store = new();
        IssueDeviceCodeUseCase useCase = new(
            MakeRegistry("google"),
            store,
            issuer: "https://claudeforge.example.com");

        DeviceCodeResponse response = await useCase.ExecuteAsync("google");

        Assert.NotNull(response.DeviceCode);
        Assert.NotEmpty(response.DeviceCode);
        Assert.NotNull(response.UserCode);
        Assert.Equal(8, response.UserCode.Length);
        Assert.Contains("/activate", response.VerificationUrl);
        Assert.True(response.ExpiresIn > 0);
        Assert.True(response.Interval > 0);
    }

    [Fact]
    public async Task ExecuteAsync_ValidProvider_StoresStateInStore()
    {
        DeviceCodeStore store = new();
        IssueDeviceCodeUseCase useCase = new(
            MakeRegistry("google"),
            store,
            issuer: "https://claudeforge.example.com");

        DeviceCodeResponse response = await useCase.ExecuteAsync("google");

        DeviceAuthState? state = store.FindByDeviceCode(response.DeviceCode);
        Assert.NotNull(state);
        Assert.Equal("google", state!.Provider);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownProvider_ThrowsUnsupportedProviderException()
    {
        DeviceCodeStore store = new();
        IssueDeviceCodeUseCase useCase = new(
            MakeRegistry("google"),
            store,
            issuer: "https://claudeforge.example.com");

        await Assert.ThrowsAsync<ClaudeForge.Core.Identity.Ports.UnsupportedProviderException>(
            () => useCase.ExecuteAsync("github"));
    }

    [Fact]
    public async Task ExecuteAsync_TwoCalls_GenerateDistinctDeviceCodes()
    {
        DeviceCodeStore store = new();
        IssueDeviceCodeUseCase useCase = new(
            MakeRegistry("google"),
            store,
            issuer: "https://claudeforge.example.com");

        DeviceCodeResponse r1 = await useCase.ExecuteAsync("google");
        DeviceCodeResponse r2 = await useCase.ExecuteAsync("google");

        Assert.NotEqual(r1.DeviceCode, r2.DeviceCode);
    }

    [Fact]
    public async Task ExecuteAsync_IssuerWithTrailingSlash_VerificationUrlHasNoDoubleSlash()
    {
        DeviceCodeStore store = new();
        IssueDeviceCodeUseCase useCase = new(
            MakeRegistry("google"),
            store,
            issuer: "https://claudeforge.example.com/");

        DeviceCodeResponse response = await useCase.ExecuteAsync("google");

        // Should not produce https://claudeforge.example.com//activate
        Assert.DoesNotContain("//activate", response.VerificationUrl);
    }
}

/// <summary>
/// Unit tests for <see cref="PollDeviceTokenUseCase"/> state machine transitions.
/// </summary>
public sealed class PollDeviceTokenUseCaseTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DeviceAuthState MakeState(
        string deviceCode = "dc-poll",
        bool expired = false,
        SignInTokens? tokens = null)
    {
        DateTimeOffset expiresAt = expired
            ? DateTimeOffset.UtcNow.AddSeconds(-1)
            : DateTimeOffset.UtcNow.AddMinutes(15);

        return new DeviceAuthState(
            DeviceCode: deviceCode,
            UserCode: "USERCODE",
            Provider: "google",
            ExpiresAt: expiresAt,
            Tokens: tokens);
    }

    // -----------------------------------------------------------------------
    // Not-found → Expired
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_UnknownDeviceCode_ReturnsExpired()
    {
        DeviceCodeStore store = new();
        PollDeviceTokenUseCase useCase = new(store);

        DeviceTokenPollResult result = await useCase.ExecuteAsync("nonexistent-code");

        Assert.IsType<DeviceTokenPollResult.Expired>(result);
    }

    // -----------------------------------------------------------------------
    // Expired code → Expired (and removed)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ExpiredCode_ReturnsExpired()
    {
        DeviceCodeStore store = new();
        store.Store(MakeState("dc-exp", expired: true));
        PollDeviceTokenUseCase useCase = new(store);

        DeviceTokenPollResult result = await useCase.ExecuteAsync("dc-exp");

        Assert.IsType<DeviceTokenPollResult.Expired>(result);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredCode_RemovesFromStore()
    {
        DeviceCodeStore store = new();
        store.Store(MakeState("dc-exp2", expired: true));
        PollDeviceTokenUseCase useCase = new(store);

        await useCase.ExecuteAsync("dc-exp2");

        Assert.Null(store.FindByDeviceCode("dc-exp2"));
    }

    // -----------------------------------------------------------------------
    // Pending (not yet approved, normal poll rate)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PendingState_ReturnsPending()
    {
        DeviceCodeStore store = new();
        store.Store(MakeState("dc-pend"));
        PollDeviceTokenUseCase useCase = new(store);

        DeviceTokenPollResult result = await useCase.ExecuteAsync("dc-pend");

        Assert.IsType<DeviceTokenPollResult.Pending>(result);
    }

    // -----------------------------------------------------------------------
    // SlowDown — second immediate poll
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_TwoImmediatePolls_SecondReturnsSlowDown()
    {
        DeviceCodeStore store = new();
        store.Store(MakeState("dc-slow"));
        PollDeviceTokenUseCase useCase = new(store);

        // First poll records timestamp
        await useCase.ExecuteAsync("dc-slow");

        // Immediate second poll must return SlowDown
        DeviceTokenPollResult result = await useCase.ExecuteAsync("dc-slow");

        Assert.IsType<DeviceTokenPollResult.SlowDown>(result);
    }

    // -----------------------------------------------------------------------
    // Approved — tokens present
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ApprovedState_ReturnsApprovedWithTokens()
    {
        SignInTokens tokens = new("access-tok", "refresh-tok", DateTimeOffset.UtcNow.AddHours(1));
        DeviceCodeStore store = new();
        store.Store(MakeState("dc-appr", tokens: tokens));
        PollDeviceTokenUseCase useCase = new(store);

        DeviceTokenPollResult result = await useCase.ExecuteAsync("dc-appr");

        DeviceTokenPollResult.Approved approved = Assert.IsType<DeviceTokenPollResult.Approved>(result);
        Assert.Equal("access-tok", approved.Tokens.AccessToken);
        Assert.Equal("refresh-tok", approved.Tokens.RefreshToken);
    }

    [Fact]
    public async Task ExecuteAsync_ApprovedState_RemovesFromStore()
    {
        SignInTokens tokens = new("at", "rt", DateTimeOffset.UtcNow.AddHours(1));
        DeviceCodeStore store = new();
        store.Store(MakeState("dc-appr2", tokens: tokens));
        PollDeviceTokenUseCase useCase = new(store);

        await useCase.ExecuteAsync("dc-appr2");

        // After approval the entry must be cleaned up
        Assert.Null(store.FindByDeviceCode("dc-appr2"));
    }
}
