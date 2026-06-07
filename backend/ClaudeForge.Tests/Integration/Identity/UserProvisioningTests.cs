using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Integration tests for Group 4, Tasks 4.5 + 4.6 — IUserStorePort provisioning
/// and verified-email account linking.
///
/// These tests are RED because the following production types DO NOT YET EXIST.
/// The coder MUST create them to turn RED → GREEN.
///
/// ─── Core port (ClaudeForge.Core.Identity.Ports) ─────────────────────────────
///
///   interface IUserStorePort
///     /// First sign-in: creates a new user + user_identity row.
///     /// Repeat sign-in: updates name and email on the existing user; does NOT create a duplicate.
///     /// Second provider with the same VERIFIED email: links via a new user_identity row on the existing user.
///     /// Second provider with an UNVERIFIED email: does NOT auto-link; creates a separate user (or throws, per config flag).
///     Task&lt;ProvisionedUser&gt; ProvisionOrLinkAsync(
///         string provider,
///         string subject,
///         string email,
///         bool emailVerified,
///         string displayName,
///         CancellationToken ct = default)
///
///   sealed record ProvisionedUser(
///     Guid   UserId,
///     string Email,
///     string DisplayName,
///     bool   IsNewUser)      — true on first provision, false on repeat sign-in/link
///
/// ─── Infrastructure (ClaudeForge.Infrastructure.Identity) ────────────────────
///
///   sealed class UserStoreAdapter : IUserStorePort
///     UserStoreAdapter(IDbContextFactory&lt;MarketplaceDbContext&gt; dbFactory, Microsoft.Extensions.Options.IOptions&lt;UserStoreOptions&gt; options)
///
///   sealed class UserStoreOptions
///     bool DisableCrossProviderLinking { get; init; }  — default false; true blocks verified-email link
///
/// ─── Linking rules (design.md §2 "Account Linking") ─────────────────────────
///   1. New (provider, subject)  → provision: create user + user_identity
///   2. Existing (provider, subject) → update user.email + user.display_name; no new rows
///   3. Different provider, same VERIFIED email → link: create user_identity row for existing user
///   4. Different provider, same UNVERIFIED email → no auto-link; create new (separate) user
///
/// ─── Test strategy ───────────────────────────────────────────────────────────
///   All tests run against the real PostgreSQL schema via PostgresFixture.
///   Each test truncates user-related tables before running to ensure isolation.
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class UserProvisioningTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public UserProvisioningTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // =========================================================================
    // Per-test isolation
    // =========================================================================

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

    // =========================================================================
    // Helpers
    // =========================================================================

    private IUserStorePort MakeAdapter(bool disableCrossProviderLinking = false)
    {
        UserStoreOptions options = new() { DisableCrossProviderLinking = disableCrossProviderLinking };
        IDbContextFactory<MarketplaceDbContext> factory = new SingletonDbContextFactory(_fixture);
        return new UserStoreAdapter(factory, Microsoft.Extensions.Options.Options.Create(options));
    }

    private static IDbContextFactory<MarketplaceDbContext> MakeFactory(PostgresFixture fixture) =>
        new SingletonDbContextFactory(fixture);

    private sealed class SingletonDbContextFactory : IDbContextFactory<MarketplaceDbContext>
    {
        private readonly PostgresFixture _fixture;

        public SingletonDbContextFactory(PostgresFixture fixture)
        {
            _fixture = fixture;
        }

        public MarketplaceDbContext CreateDbContext() => _fixture.CreateContext();
    }

    // =========================================================================
    // Rule 1 — new (provider, subject) → provision user + user_identity
    // =========================================================================

    [Fact]
    public async Task ProvisionOrLink_NewUser_CreatesUserAndIdentity()
    {
        // Arrange
        IUserStorePort store = MakeAdapter();

        // Act
        ProvisionedUser result = await store.ProvisionOrLinkAsync(
            provider: "google",
            subject: "google-sub-new-001",
            email: "alice@example.com",
            emailVerified: true,
            displayName: "Alice");

        // Assert — returns a valid user ID
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal("alice@example.com", result.Email);
        Assert.Equal("Alice", result.DisplayName);
        Assert.True(result.IsNewUser);

        // Verify persistence: user row exists
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity? user = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == result.UserId);
        Assert.NotNull(user);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("Alice", user.DisplayName);

        // Verify persistence: user_identity row exists
        UserIdentityEntity? identity = await ctx.UserIdentities.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Provider == "google" && i.Subject == "google-sub-new-001");
        Assert.NotNull(identity);
        Assert.Equal(result.UserId, identity.UserId);
    }

    [Fact]
    public async Task ProvisionOrLink_NewUser_EmailNormalizedIsLowerCase()
    {
        // Arrange
        IUserStorePort store = MakeAdapter();

        // Act — email with mixed case
        ProvisionedUser result = await store.ProvisionOrLinkAsync(
            provider: "google",
            subject: "google-sub-email-case",
            email: "Carol@Example.COM",
            emailVerified: true,
            displayName: "Carol");

        // Assert — email_normalized must be lower-case (unique key enforcement)
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        UserEntity? user = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == result.UserId);
        Assert.NotNull(user);
        Assert.Equal("carol@example.com", user.EmailNormalized);
    }

    // =========================================================================
    // Rule 2 — repeat sign-in → updates name/email, no duplicate user
    // =========================================================================

    [Fact]
    public async Task ProvisionOrLink_RepeatSignIn_UpdatesNameAndEmailWithoutDuplicate()
    {
        // Arrange — first sign-in creates the user
        IUserStorePort store = MakeAdapter();
        ProvisionedUser first = await store.ProvisionOrLinkAsync(
            "google", "google-sub-repeat-001",
            "bob@example.com", true, "Bob Old Name");

        // Act — second sign-in for the same (provider, subject) with updated name/email
        ProvisionedUser second = await store.ProvisionOrLinkAsync(
            "google", "google-sub-repeat-001",
            "bob.new@example.com", true, "Bob New Name");

        // Assert — same user ID returned
        Assert.Equal(first.UserId, second.UserId);
        Assert.False(second.IsNewUser);
        Assert.Equal("bob.new@example.com", second.Email);
        Assert.Equal("Bob New Name", second.DisplayName);

        // Verify: only ONE user row for this identity
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        int userCount = await ctx.Users.CountAsync(u => u.Id == first.UserId);
        Assert.Equal(1, userCount);

        // Verify: only ONE user_identity row for (google, google-sub-repeat-001)
        int identityCount = await ctx.UserIdentities
            .CountAsync(i => i.Provider == "google" && i.Subject == "google-sub-repeat-001");
        Assert.Equal(1, identityCount);

        // Verify: updated values persisted
        UserEntity? user = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == first.UserId);
        Assert.NotNull(user);
        Assert.Equal("bob.new@example.com", user.Email);
        Assert.Equal("Bob New Name", user.DisplayName);
    }

    [Fact]
    public async Task ProvisionOrLink_RepeatSignIn_MultipleTimes_NeverCreatesNewUser()
    {
        // Arrange — sign in 5 times for the same identity
        IUserStorePort store = MakeAdapter();
        ProvisionedUser first = await store.ProvisionOrLinkAsync(
            "microsoft", "ms-sub-multi", "dave@example.com", true, "Dave");

        // Act — sign in 4 more times
        for (int i = 0; i < 4; i++)
        {
            await store.ProvisionOrLinkAsync(
                "microsoft", "ms-sub-multi", "dave@example.com", true, $"Dave v{i + 2}");
        }

        // Assert — database still has exactly 1 user and 1 identity
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        int totalUsersForEmail = await ctx.Users
            .CountAsync(u => u.EmailNormalized == "dave@example.com");
        Assert.Equal(1, totalUsersForEmail);

        int identityCount = await ctx.UserIdentities
            .CountAsync(i => i.Provider == "microsoft" && i.Subject == "ms-sub-multi");
        Assert.Equal(1, identityCount);
    }

    // =========================================================================
    // Rule 3 — different provider, VERIFIED email → link via user_identities
    // =========================================================================

    [Fact]
    public async Task ProvisionOrLink_SecondProvider_VerifiedEmail_LinksToExistingUser()
    {
        // Arrange — user signs in with Google first
        IUserStorePort store = MakeAdapter();
        ProvisionedUser googleUser = await store.ProvisionOrLinkAsync(
            provider: "google",
            subject: "google-sub-link-001",
            email: "eve@example.com",
            emailVerified: true,
            displayName: "Eve");

        // Act — same email, different provider (Microsoft), email verified
        ProvisionedUser microsoftUser = await store.ProvisionOrLinkAsync(
            provider: "microsoft",
            subject: "ms-sub-link-001",
            email: "eve@example.com",
            emailVerified: true,
            displayName: "Eve via MS");

        // Assert — linked to the same user
        Assert.Equal(googleUser.UserId, microsoftUser.UserId);
        Assert.False(microsoftUser.IsNewUser);

        // Verify: two user_identity rows both point to the same user
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        List<UserIdentityEntity> identities = await ctx.UserIdentities
            .AsNoTracking()
            .Where(i => i.UserId == googleUser.UserId)
            .ToListAsync();
        Assert.Equal(2, identities.Count);
        Assert.Contains(identities, i => i.Provider == "google" && i.Subject == "google-sub-link-001");
        Assert.Contains(identities, i => i.Provider == "microsoft" && i.Subject == "ms-sub-link-001");
    }

    [Fact]
    public async Task ProvisionOrLink_ThirdProvider_VerifiedEmail_AlsoLinksToSameUser()
    {
        // Arrange — two providers already linked
        IUserStorePort store = MakeAdapter();
        ProvisionedUser first = await store.ProvisionOrLinkAsync(
            "google", "google-sub-triple", "frank@example.com", true, "Frank");
        await store.ProvisionOrLinkAsync(
            "microsoft", "ms-sub-triple", "frank@example.com", true, "Frank");

        // Suppose a future provider "github" gets added — same email should still link
        // (The test uses a fake third provider name to validate the linking logic is provider-agnostic)
        ProvisionedUser third = await store.ProvisionOrLinkAsync(
            "github", "gh-sub-triple", "frank@example.com", true, "Frank");

        // Assert — all linked to the original user
        Assert.Equal(first.UserId, third.UserId);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        int identityCount = await ctx.UserIdentities
            .CountAsync(i => i.UserId == first.UserId);
        Assert.Equal(3, identityCount);
    }

    // =========================================================================
    // Rule 4 — UNVERIFIED email → no auto-link, creates separate user
    // =========================================================================

    [Fact]
    public async Task ProvisionOrLink_SecondProvider_UnverifiedEmail_CreatesNewUser()
    {
        // Arrange — first sign-in with Google, verified email
        IUserStorePort store = MakeAdapter();
        ProvisionedUser googleUser = await store.ProvisionOrLinkAsync(
            provider: "google",
            subject: "google-sub-unverified",
            email: "grace@example.com",
            emailVerified: true,
            displayName: "Grace");

        // Act — different provider, same email but UNVERIFIED
        ProvisionedUser unverifiedUser = await store.ProvisionOrLinkAsync(
            provider: "microsoft",
            subject: "ms-sub-unverified-email",
            email: "grace@example.com",
            emailVerified: false, // unverified!
            displayName: "Grace MS");

        // Assert — must NOT link to the Google user; must create a distinct user
        Assert.NotEqual(googleUser.UserId, unverifiedUser.UserId);
        Assert.True(unverifiedUser.IsNewUser);

        // Verify: two separate users with the same email exist
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        int userCount = await ctx.Users.CountAsync(u => u.EmailNormalized == "grace@example.com");
        // Design allows separate users for unverified same email
        Assert.True(userCount >= 1, "At least the unverified user should be persisted");

        // Verify: Google user's identity list does NOT include the Microsoft subject
        bool googleUserHasMsIdentity = await ctx.UserIdentities
            .AnyAsync(i => i.UserId == googleUser.UserId && i.Provider == "microsoft");
        Assert.False(googleUserHasMsIdentity, "Google user must not have a Microsoft identity linked via unverified email");
    }

    [Fact]
    public async Task ProvisionOrLink_FirstSignIn_UnverifiedEmail_StillCreatesUser()
    {
        // Arrange — brand new user with unverified email
        IUserStorePort store = MakeAdapter();

        // Act — unverified email is acceptable for first sign-in (just no cross-provider linking)
        ProvisionedUser user = await store.ProvisionOrLinkAsync(
            provider: "google",
            subject: "google-sub-first-unverified",
            email: "henry@example.com",
            emailVerified: false,
            displayName: "Henry");

        // Assert — user is created successfully
        Assert.NotEqual(Guid.Empty, user.UserId);
        Assert.True(user.IsNewUser);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        bool exists = await ctx.Users.AnyAsync(u => u.Id == user.UserId);
        Assert.True(exists);
    }

    // =========================================================================
    // DisableCrossProviderLinking config flag
    // =========================================================================

    [Fact]
    public async Task ProvisionOrLink_DisableCrossProviderLinking_VerifiedEmail_CreatesNewUser()
    {
        // Arrange — flag is ON
        IUserStorePort store = MakeAdapter(disableCrossProviderLinking: true);
        ProvisionedUser googleUser = await store.ProvisionOrLinkAsync(
            "google", "google-sub-flag-test",
            "iris@example.com", true, "Iris");

        // Act — second provider, verified email, but linking is disabled
        ProvisionedUser msUser = await store.ProvisionOrLinkAsync(
            "microsoft", "ms-sub-flag-test",
            "iris@example.com",
            emailVerified: true,
            displayName: "Iris MS");

        // Assert — separate user created even though email is verified (flag disables linking)
        Assert.NotEqual(googleUser.UserId, msUser.UserId);
        Assert.True(msUser.IsNewUser);
    }

    // =========================================================================
    // Edge cases
    // =========================================================================

    [Fact]
    public async Task ProvisionOrLink_TwoNewUsersWithDifferentEmails_BothCreated()
    {
        // Arrange
        IUserStorePort store = MakeAdapter();

        // Act
        ProvisionedUser user1 = await store.ProvisionOrLinkAsync(
            "google", "google-sub-two-a", "jack@example.com", true, "Jack");
        ProvisionedUser user2 = await store.ProvisionOrLinkAsync(
            "google", "google-sub-two-b", "kate@example.com", true, "Kate");

        // Assert — two distinct users
        Assert.NotEqual(user1.UserId, user2.UserId);
        Assert.True(user1.IsNewUser);
        Assert.True(user2.IsNewUser);

        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        int totalUsers = await ctx.Users.CountAsync(
            u => u.Id == user1.UserId || u.Id == user2.UserId);
        Assert.Equal(2, totalUsers);
    }

    [Fact]
    public async Task ProvisionOrLink_IdentityLookup_ByProviderAndSubjectCombined()
    {
        // Arrange — two providers with same subject string but different providers
        IUserStorePort store = MakeAdapter();

        // Same subject string "shared-sub", different providers → different identities
        ProvisionedUser googleUser = await store.ProvisionOrLinkAsync(
            "google", "shared-sub", "lena@example.com", true, "Lena Google");
        ProvisionedUser msUser = await store.ProvisionOrLinkAsync(
            "microsoft", "shared-sub", "mark@example.com", true, "Mark MS");

        // Assert — different users (subject "shared-sub" is not unique across providers)
        Assert.NotEqual(googleUser.UserId, msUser.UserId);

        // Each user has their own identity row
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        bool googleIdentityExists = await ctx.UserIdentities.AnyAsync(
            i => i.Provider == "google" && i.Subject == "shared-sub" && i.UserId == googleUser.UserId);
        bool msIdentityExists = await ctx.UserIdentities.AnyAsync(
            i => i.Provider == "microsoft" && i.Subject == "shared-sub" && i.UserId == msUser.UserId);

        Assert.True(googleIdentityExists);
        Assert.True(msIdentityExists);
    }

    [Fact]
    public async Task ProvisionOrLink_NullOrEmptyDisplayName_StillCreatesUser()
    {
        // Edge case: displayName may be empty from IdP (some tokens omit name claim)
        IUserStorePort store = MakeAdapter();

        ProvisionedUser user = await store.ProvisionOrLinkAsync(
            "google", "google-sub-empty-name",
            "noname@example.com", true,
            displayName: string.Empty);

        // Assert — user is created; displayName may be empty or derived from email
        Assert.NotEqual(Guid.Empty, user.UserId);
        Assert.True(user.IsNewUser);
    }

    [Fact]
    public async Task ProvisionOrLink_ConcurrentFirstSignIn_SameIdentity_OnlyOneUserCreated()
    {
        // Race-condition guard: concurrent first sign-in for the same (provider, subject)
        // must not create duplicate users (UNIQUE constraint on user_identities.provider+subject).
        IUserStorePort store = MakeAdapter();

        Task<ProvisionedUser>[] tasks = Enumerable.Range(0, 5)
            .Select(_ => store.ProvisionOrLinkAsync(
                "google", "google-sub-concurrent",
                "concurrent@example.com", true, "Concurrent"))
            .ToArray();

        // One succeeds; others may throw (UNIQUE violation) or also succeed with same userId.
        ProvisionedUser[] results = await Task.WhenAll(tasks
            .Select(t => t.ContinueWith(task =>
                task.IsCompletedSuccessfully ? task.Result : null)));

        ProvisionedUser[] successfulResults = results
            .Where(r => r is not null)
            .ToArray()!;

        // All successful results must point to the same user
        Guid[] distinctUserIds = successfulResults
            .Select(r => r.UserId)
            .Distinct()
            .ToArray();
        Assert.Single(distinctUserIds);

        // Database must have exactly one user and one identity
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        int userCount = await ctx.Users.CountAsync(u => u.EmailNormalized == "concurrent@example.com");
        Assert.Equal(1, userCount);

        int identityCount = await ctx.UserIdentities
            .CountAsync(i => i.Provider == "google" && i.Subject == "google-sub-concurrent");
        Assert.Equal(1, identityCount);
    }

    // =========================================================================
    // ProvisionedUser record — contract
    // =========================================================================

    [Fact]
    public void ProvisionedUser_IsImmutableRecord()
    {
        // Arrange & Act
        ProvisionedUser user = new(Guid.NewGuid(), "u@example.com", "User", true);

        // Assert
        Assert.Equal("u@example.com", user.Email);
        Assert.Equal("User", user.DisplayName);
        Assert.True(user.IsNewUser);
    }

    [Fact]
    public void ProvisionedUser_EqualityByValues()
    {
        Guid id = Guid.NewGuid();
        ProvisionedUser a = new(id, "a@b.com", "A", false);
        ProvisionedUser b = new(id, "a@b.com", "A", false);
        Assert.Equal(a, b);
    }
}
