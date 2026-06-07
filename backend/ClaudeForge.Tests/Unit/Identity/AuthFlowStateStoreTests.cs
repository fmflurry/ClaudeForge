using ClaudeForge.Core.Identity.Ports;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for Task 5.3 — PKCE/state server-side store.
///
/// These tests verify the contract of <see cref="IAuthFlowStatePort"/>, which stores
/// PKCE code_verifier + state for the web authorize/callback flow.  The store must be:
///   - Short-lived (configurable TTL; tests use a short TTL to verify expiry)
///   - Single-use (consumed once, then gone)
///   - Unknown/expired state rejected (returns null)
///
/// ═══════════════════════════════════════════════════════════════════════════════
/// Contract the coder MUST create
///
///   NAMESPACE: ClaudeForge.Core.Identity.Ports
///
///   sealed record AuthFlowState(
///     string State,              — opaque, random, URL-safe string (min 32 chars)
///     string CodeVerifier,       — PKCE verifier (43-128 URL-safe chars per RFC 7636)
///     string Provider,           — "google" | "microsoft" (or any enabled provider name)
///     string RedirectUri,        — exact redirect_uri used in BuildAuthorizationUrl
///     DateTimeOffset ExpiresAt); — UTC expiry
///
///   interface IAuthFlowStatePort
///     /// Stores a new auth flow state. Replaces any prior entry with the same state key.
///     Task StoreAsync(AuthFlowState entry, CancellationToken ct = default);
///
///     /// Returns and DELETES the state entry (single-use).
///     /// Returns null if the state is unknown or has expired.
///     Task&lt;AuthFlowState?&gt; ConsumeAsync(string state, CancellationToken ct = default);
///
///   IMPLEMENTATION NOTE: the coder may choose either:
///     (a) An in-memory ConcurrentDictionary with a background sweep (simplest for now)
///     (b) A Postgres table with TTL predicate on ConsumeAsync
///   For tests, a concrete in-memory implementation (InMemoryAuthFlowStateStore) in the
///   Infrastructure or Core assembly is sufficient.  The class is:
///
///   NAMESPACE: ClaudeForge.Infrastructure.Identity
///
///   sealed class InMemoryAuthFlowStateStore : IAuthFlowStatePort
///     Constructor: InMemoryAuthFlowStateStore(TimeProvider timeProvider)
///       — uses TimeProvider for testable clock (defaults to TimeProvider.System in DI)
///
/// ═══════════════════════════════════════════════════════════════════════════════
/// </summary>
public sealed class AuthFlowStateStoreTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Factory: use a fake TimeProvider so we can advance the clock in tests.
    // ─────────────────────────────────────────────────────────────────────────

    private static (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore Store, FakeTimeProvider Clock)
        MakeStore()
    {
        FakeTimeProvider clock = new();
        return (new ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore(clock), clock);
    }

    private static AuthFlowState MakeEntry(
        string? state = null,
        string? codeVerifier = null,
        string provider = "google",
        string redirectUri = "https://app.example.com/auth/callback",
        DateTimeOffset? expiresAt = null)
    {
        return new AuthFlowState(
            State: state ?? Guid.NewGuid().ToString("N"),
            CodeVerifier: codeVerifier ?? GenerateVerifier(),
            Provider: provider,
            RedirectUri: redirectUri,
            ExpiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5));
    }

    private static string GenerateVerifier()
    {
        // RFC 7636: 43-128 URL-safe chars
        byte[] bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Happy path — store then consume returns the entry
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_AfterStore_ReturnsEntry()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();
        AuthFlowState entry = MakeEntry();

        await store.StoreAsync(entry);
        AuthFlowState? consumed = await store.ConsumeAsync(entry.State);

        Assert.NotNull(consumed);
        Assert.Equal(entry.State, consumed!.State);
        Assert.Equal(entry.CodeVerifier, consumed.CodeVerifier);
        Assert.Equal(entry.Provider, consumed.Provider);
        Assert.Equal(entry.RedirectUri, consumed.RedirectUri);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Single-use: consumed once, second consume returns null
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_CalledTwice_SecondCallReturnsNull()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();
        AuthFlowState entry = MakeEntry();

        await store.StoreAsync(entry);
        AuthFlowState? first = await store.ConsumeAsync(entry.State);
        AuthFlowState? second = await store.ConsumeAsync(entry.State);

        Assert.NotNull(first);
        Assert.Null(second); // gone after first consume
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unknown state returns null (no panic, no exception)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_UnknownState_ReturnsNull()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();

        AuthFlowState? result = await store.ConsumeAsync("state-that-was-never-stored");

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Expired entry returns null (TTL honored)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_ExpiredEntry_ReturnsNull()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, FakeTimeProvider clock) = MakeStore();

        // Store an entry that expires in 1 minute
        DateTimeOffset expiresAt = clock.GetUtcNow().AddMinutes(1);
        AuthFlowState entry = MakeEntry(expiresAt: expiresAt);

        await store.StoreAsync(entry);

        // Advance clock past expiry
        clock.Advance(TimeSpan.FromMinutes(2));

        AuthFlowState? result = await store.ConsumeAsync(entry.State);

        Assert.Null(result); // expired — must not be returned
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Entry not yet expired is still returned
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_NotYetExpired_ReturnsEntry()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, FakeTimeProvider clock) = MakeStore();

        DateTimeOffset expiresAt = clock.GetUtcNow().AddMinutes(5);
        AuthFlowState entry = MakeEntry(expiresAt: expiresAt);

        await store.StoreAsync(entry);

        // Advance clock by less than the TTL
        clock.Advance(TimeSpan.FromMinutes(4));

        AuthFlowState? result = await store.ConsumeAsync(entry.State);

        Assert.NotNull(result);
        Assert.Equal(entry.State, result!.State);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Multiple entries are isolated: consuming one doesn't affect others
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsumeAsync_OneEntry_DoesNotAffectOtherEntries()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();
        AuthFlowState a = MakeEntry();
        AuthFlowState b = MakeEntry();

        await store.StoreAsync(a);
        await store.StoreAsync(b);

        // Consume only `a`
        await store.ConsumeAsync(a.State);

        // `b` must still be retrievable
        AuthFlowState? bResult = await store.ConsumeAsync(b.State);
        Assert.NotNull(bResult);
        Assert.Equal(b.State, bResult!.State);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Re-storing with the same state key replaces the entry
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreAsync_SameStateKey_ReplacesExistingEntry()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();
        string sharedState = "shared-state-key";
        AuthFlowState original = MakeEntry(state: sharedState, codeVerifier: "original-verifier");
        AuthFlowState replacement = MakeEntry(state: sharedState, codeVerifier: "replacement-verifier");

        await store.StoreAsync(original);
        await store.StoreAsync(replacement);  // replaces original

        AuthFlowState? result = await store.ConsumeAsync(sharedState);

        Assert.NotNull(result);
        Assert.Equal("replacement-verifier", result!.CodeVerifier);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ConsumeAsync with empty/whitespace state returns null (no exception)
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConsumeAsync_EmptyOrWhitespaceState_ReturnsNull(string state)
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();

        AuthFlowState? result = await store.ConsumeAsync(state);

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Large number of concurrent stores: isolation between entries
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreAsync_ManyEntries_AllConsumedCorrectly()
    {
        (ClaudeForge.Infrastructure.Identity.InMemoryAuthFlowStateStore store, _) = MakeStore();

        const int count = 200;
        AuthFlowState[] entries = Enumerable.Range(0, count)
            .Select(_ => MakeEntry())
            .ToArray();

        // Store all concurrently
        await Task.WhenAll(entries.Select(e => store.StoreAsync(e)));

        // Consume all — each must be found exactly once
        int found = 0;
        foreach (AuthFlowState entry in entries)
        {
            AuthFlowState? result = await store.ConsumeAsync(entry.State);
            if (result is not null) found++;
        }

        Assert.Equal(count, found);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// FakeTimeProvider — controllable clock for unit tests.
// Required because InMemoryAuthFlowStateStore must accept a TimeProvider to be testable.
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);

    public void SetUtcNow(DateTimeOffset value) => _now = value;
}
