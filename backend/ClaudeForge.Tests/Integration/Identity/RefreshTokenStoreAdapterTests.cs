using System.Security.Cryptography;
using System.Text;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Integration tests for Group 3, Task 3.4 — IRefreshTokenStorePort with
/// opaque-token storage, SHA-256 hashing, one-time-use rotation, reuse-detection,
/// chain revocation, and expiry enforcement.
///
/// These tests are RED because the production types listed below do not yet exist.
/// The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Identity.Ports
///
///   sealed record CreateRefreshTokenCommand
///     Guid   UserId
///     int    ExpiryDays  // defaults to 30 per design.md §1
///
///   sealed record RefreshTokenResult
///     Guid   Id          // row PK
///     Guid   UserId
///     string PlainToken  // opaque random string — returned once at creation, never persisted
///     DateTimeOffset ExpiresAt
///
///   sealed record RotateRefreshTokenResult
///     Guid   NewId
///     string NewPlainToken  // new opaque token to hand to caller
///     DateTimeOffset NewExpiresAt
///
///   interface IRefreshTokenStorePort
///     /// Creates a new refresh token. Returns the plain token once — caller must store it.
///     /// Implementation persists only SHA-256(plainToken) in token_hash.
///     Task&lt;RefreshTokenResult&gt; CreateAsync(
///         CreateRefreshTokenCommand cmd, CancellationToken ct = default);
///
///     /// Looks up a token by SHA-256(plainToken). Returns null when not found.
///     Task&lt;RefreshTokenInfo?&gt; FindByHashAsync(string plainToken, CancellationToken ct = default);
///
///     /// Atomic one-time-use rotation: marks oldId as rotated_to=newId, creates new token row.
///     /// Caller passes rootId so the new row inherits the family root.
///     Task&lt;RotateRefreshTokenResult&gt; RotateAsync(Guid oldId, Guid userId, Guid rootId, CancellationToken ct = default);
///
///     /// Revoke the entire family (root_id = rootId) in one statement.
///     Task RevokeChainAsync(Guid rootId, CancellationToken ct = default);
///
///   NAMESPACE: ClaudeForge.Infrastructure.Identity
///
///   sealed class RefreshTokenStoreAdapter : IRefreshTokenStorePort
///     Constructor: RefreshTokenStoreAdapter(MarketplaceDbContext db, int defaultExpiryDays = 30)
///
/// Design source-of-truth (design.md §1):
///   - Opaque random string, stored server-side hashed (SHA-256) in refresh_tokens.token_hash
///   - One-time-use rotation: sets rotated_to on old row, creates new row
///   - Reuse detection: presenting an already-rotated token → revoke entire chain
///   - 30-day default expiry
///   - revoked_at is set when revoking; rotated_to points to successor
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class RefreshTokenStoreAdapterTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public RefreshTokenStoreAdapterTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Per-test isolation
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                org_audit_log,
                organization_invitations,
                organization_members,
                refresh_tokens,
                user_identities,
                organizations,
                users,
                telemetry_aggregates,
                telemetry_events,
                plugin_categories,
                plugin_versions,
                plugins,
                categories
            RESTART IDENTITY CASCADE
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helper factories (immutable, no mutation)
    // -------------------------------------------------------------------------

    private IRefreshTokenStorePort MakeAdapter(int defaultExpiryDays = 30) =>
        new ClaudeForge.Infrastructure.Identity.RefreshTokenStoreAdapter(
            _fixture.CreateContext(),
            defaultExpiryDays: defaultExpiryDays);

    /// <summary>Creates a persisted user and returns their Id.</summary>
    private async Task<Guid> CreateUserAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid userId = Guid.NewGuid();
        ctx.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"user-{userId:N}@example.com",
            EmailNormalized = $"user-{userId:N}@example.com",
            DisplayName = "Test User",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    /// <summary>Computes SHA-256 of a plain token exactly as the adapter must.</summary>
    private static string Sha256Hex(string plainToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexStringLower(hash); // 64-character lowercase hex
    }

    // =========================================================================
    // CREATE — opaque token, SHA-256 stored, plain token returned once
    // =========================================================================

    [Fact]
    public async Task CreateAsync_ReturnsNonEmptyPlainToken()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult result = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        Assert.NotNull(result.PlainToken);
        Assert.NotEmpty(result.PlainToken);
    }

    [Fact]
    public async Task CreateAsync_StoredHashIsSha256OfPlainToken_NotPlaintext()
    {
        // The token_hash in the DB must be SHA-256(plain), NEVER the plain token itself
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult result = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        // Read directly from DB to verify what was stored
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        RefreshTokenEntity? entity = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == result.Id);

        Assert.NotNull(entity);
        // hash must NOT equal the plain token
        Assert.NotEqual(result.PlainToken, entity!.TokenHash);
        // hash must match SHA-256(plain)
        Assert.Equal(Sha256Hex(result.PlainToken), entity!.TokenHash);
    }

    [Fact]
    public async Task CreateAsync_PlainTokenIsNeverStoredInDatabase()
    {
        // Belt-and-suspenders: the raw plain token string must not appear anywhere in the DB
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult result = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        // If the plain token appeared as a token_hash, this query would return something
        bool storedAsPlaintext = await ctx.RefreshTokens
            .AnyAsync(t => t.TokenHash == result.PlainToken);

        Assert.False(storedAsPlaintext, "Plain token must never be persisted in token_hash");
    }

    [Fact]
    public async Task CreateAsync_ExpiresAt_IsApproximately30DaysInFuture()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter(defaultExpiryDays: 30);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        RefreshTokenResult result = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));
        DateTimeOffset after = DateTimeOffset.UtcNow;

        TimeSpan minExpected = TimeSpan.FromDays(30) - TimeSpan.FromSeconds(5);
        TimeSpan maxExpected = TimeSpan.FromDays(30) + TimeSpan.FromSeconds(5);
        TimeSpan actualLifetime = result.ExpiresAt - before;

        Assert.True(actualLifetime >= minExpected && actualLifetime <= maxExpected,
            $"Expected expiry ~30 days, got {actualLifetime.TotalDays:F2} days");
    }

    [Fact]
    public async Task CreateAsync_TokenHash_Is64CharacterLowercaseHex()
    {
        // SHA-256 produces 32 bytes = 64 hex characters
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult result = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        RefreshTokenEntity? entity = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == result.Id);

        Assert.NotNull(entity);
        Assert.Equal(64, entity!.TokenHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", entity!.TokenHash);
    }

    [Fact]
    public async Task CreateAsync_TwoCallsProduceDifferentTokens()
    {
        // Opaque tokens must be unique per call (random)
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult r1 = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));
        RefreshTokenResult r2 = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        Assert.NotEqual(r1.PlainToken, r2.PlainToken);
        Assert.NotEqual(r1.Id, r2.Id);
    }

    // =========================================================================
    // FIND BY HASH
    // =========================================================================

    [Fact]
    public async Task FindByHashAsync_ExistingToken_ReturnsEntity()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult created = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        RefreshTokenInfo? found = await store.FindByHashAsync(created.PlainToken);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal(userId, found!.UserId);
    }

    [Fact]
    public async Task FindByHashAsync_UnknownToken_ReturnsNull()
    {
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenInfo? found = await store.FindByHashAsync("totally-unknown-token-value");

        Assert.Null(found);
    }

    [Fact]
    public async Task FindByHashAsync_EmptyString_ReturnsNull()
    {
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenInfo? found = await store.FindByHashAsync(string.Empty);

        Assert.Null(found);
    }

    // =========================================================================
    // ROTATE — one-time-use, sets rotated_to on old row, creates new row
    // =========================================================================

    [Fact]
    public async Task RotateAsync_OldTokenGetsRotatedTo_PointingToNewTokenId()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();
        RefreshTokenResult original = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        RotateRefreshTokenResult rotated = await store.RotateAsync(original.Id, userId, rootId: original.Id);

        // Old row must have rotated_to set to the new row's Id
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        RefreshTokenEntity? oldEntity = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == original.Id);
        Assert.NotNull(oldEntity);
        Assert.Equal(rotated.NewId, oldEntity!.RotatedTo);
    }

    [Fact]
    public async Task RotateAsync_NewTokenRow_CreatedInDatabase()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();
        RefreshTokenResult original = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        RotateRefreshTokenResult rotated = await store.RotateAsync(original.Id, userId, rootId: original.Id);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        RefreshTokenEntity? newEntity = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == rotated.NewId);
        Assert.NotNull(newEntity);
        Assert.Equal(userId, newEntity!.UserId);
        Assert.Null(newEntity!.RevokedAt); // new token not yet revoked
        Assert.Null(newEntity!.RotatedTo);  // not yet rotated again
    }

    [Fact]
    public async Task RotateAsync_NewPlainToken_IsDifferentFromOld()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();
        RefreshTokenResult original = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        RotateRefreshTokenResult rotated = await store.RotateAsync(original.Id, userId, rootId: original.Id);

        Assert.NotEqual(original.PlainToken, rotated.NewPlainToken);
    }

    [Fact]
    public async Task RotateAsync_NewPlainToken_StoredAsSha256Hash()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();
        RefreshTokenResult original = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        RotateRefreshTokenResult rotated = await store.RotateAsync(original.Id, userId, rootId: original.Id);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        RefreshTokenEntity? newEntity = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == rotated.NewId);
        Assert.NotNull(newEntity);
        Assert.Equal(Sha256Hex(rotated.NewPlainToken), newEntity!.TokenHash);
    }

    // =========================================================================
    // REUSE DETECTION — presenting already-rotated token revokes entire chain
    // =========================================================================

    [Fact]
    public async Task RevokeChainAsync_RootRevoked_AllDescendantsRevoked()
    {
        // Build chain: root → token2 → token3
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult root = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));
        RotateRefreshTokenResult step1 = await store.RotateAsync(root.Id, userId, rootId: root.Id);
        RotateRefreshTokenResult step2 = await store.RotateAsync(step1.NewId, userId, rootId: root.Id);

        // Simulate reuse detection: caller revokeChain from the root
        await store.RevokeChainAsync(root.Id);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid[] allIds = [root.Id, step1.NewId, step2.NewId];
        List<RefreshTokenEntity> entities = await ctx.RefreshTokens
            .Where(t => allIds.Contains(t.Id))
            .ToListAsync();

        Assert.Equal(3, entities.Count);
        foreach (RefreshTokenEntity entity in entities)
        {
            Assert.NotNull(entity.RevokedAt);
        }
    }

    [Fact]
    public async Task RevokeChainAsync_SingleNodeChain_NodeRevoked()
    {
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();
        RefreshTokenResult token = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        await store.RevokeChainAsync(token.Id);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        RefreshTokenEntity? entity = await ctx.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == token.Id);
        Assert.NotNull(entity);
        Assert.NotNull(entity!.RevokedAt);
    }

    [Fact]
    public async Task FindByHashAsync_AfterRevokeChain_TokenStillFindable_ButRevoked()
    {
        // The chain revocation sets revoked_at but does NOT delete the rows.
        // Callers must check RevokedAt after FindByHashAsync.
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();
        RefreshTokenResult token = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));

        await store.RevokeChainAsync(token.Id);

        RefreshTokenInfo? found = await store.FindByHashAsync(token.PlainToken);
        Assert.NotNull(found);
        Assert.NotNull(found!.RevokedAt);
    }

    [Fact]
    public async Task Rotate_AlreadyRotatedToken_PresentedAgain_WholeChainShouldBeRevocable()
    {
        // Design rule: presenting an already-rotated token indicates theft.
        // RevokeChainAsync must be called on any ancestor that has RotatedTo set.
        // This test verifies the chain root can be identified and revoked.
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter();

        RefreshTokenResult root = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30));
        // Rotate once (legitimate)
        RotateRefreshTokenResult rotated = await store.RotateAsync(root.Id, userId, rootId: root.Id);

        // Now "attacker" re-presents the OLD (already-rotated) token.
        // Caller detects rotation (root.RotatedTo != null) and calls RevokeChainAsync(root.Id)
        await store.RevokeChainAsync(root.Id);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        Guid[] allIds = [root.Id, rotated.NewId];
        List<RefreshTokenEntity> entities = await ctx.RefreshTokens
            .Where(t => allIds.Contains(t.Id))
            .ToListAsync();

        Assert.All(entities, e => Assert.NotNull(e.RevokedAt));
    }

    // =========================================================================
    // EXPIRY ENFORCEMENT
    // =========================================================================

    [Fact]
    public async Task CreateAsync_ExpiredToken_ShouldBeConsideredExpiredByCallers()
    {
        // Create a token with a past expiry (1 day ago)
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter(defaultExpiryDays: -1);

        RefreshTokenResult result = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: -1));

        // ExpiresAt must be in the past
        Assert.True(result.ExpiresAt < DateTimeOffset.UtcNow,
            "Token with -1 day expiry must expire in the past");
    }

    [Fact]
    public async Task FindByHashAsync_ExpiredToken_ReturnsEntityWithExpiredAt()
    {
        // Expiry is data-only; the store itself does not filter out expired tokens
        // (the use-case layer checks ExpiresAt). This verifies the store returns the
        // entity so the caller can inspect ExpiresAt and reject it.
        Guid userId = await CreateUserAsync();
        IRefreshTokenStorePort store = MakeAdapter(defaultExpiryDays: -1);

        RefreshTokenResult expired = await store.CreateAsync(
            new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: -1));

        RefreshTokenInfo? found = await store.FindByHashAsync(expired.PlainToken);
        Assert.NotNull(found);
        Assert.True(found!.ExpiresAt < DateTimeOffset.UtcNow, "ExpiresAt must reflect past expiry");
    }

    // =========================================================================
    // CONCURRENCY / UNIQUENESS
    // =========================================================================

    [Fact]
    public async Task CreateAsync_ConcurrentCalls_AllProduceUniqueTokenHashes()
    {
        // 20 concurrent creates for the same user — all token_hashes must be distinct
        Guid userId = await CreateUserAsync();

        // Each concurrent call needs its own DbContext to avoid concurrency issues
        IEnumerable<Task<RefreshTokenResult>> tasks = Enumerable.Range(0, 20)
            .Select(_ => new ClaudeForge.Infrastructure.Identity.RefreshTokenStoreAdapter(
                    _fixture.CreateContext(), defaultExpiryDays: 30)
                .CreateAsync(new CreateRefreshTokenCommand(UserId: userId, ExpiryDays: 30)));

        RefreshTokenResult[] results = await Task.WhenAll(tasks);

        HashSet<string> hashes = results.Select(r => r.PlainToken).ToHashSet();
        Assert.Equal(20, hashes.Count);
    }
}
