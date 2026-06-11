using ClaudeForge.Core.Shared.Authorization;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Authorization;

/// <summary>
/// Unit tests for Group 2, Task 2.2 — IAddOnAccessPolicy read/download decision matrix.
///
/// These tests are RED because the production contracts do not yet exist.
/// The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Shared.Authorization
///
///   enum AccessDecision
///     Allow          — caller is permitted (HTTP 200)
///     NotFound       — private plugin + authenticated non-member (HTTP 404 non-disclosure)
///     Unauthenticated — private plugin + anonymous caller (HTTP 401)
///     Forbidden      — authenticated caller attempting disallowed write (HTTP 403)
///
///   interface ICurrentUser
///     Guid?  UserId          — null when anonymous
///     bool   IsAuthenticated — true when UserId is non-null and token valid
///     string? Email          — null when anonymous
///
///   interface IAddOnAccessPolicy
///     /// Pure domain service — no I/O, deterministic.
///     AccessDecision DecideRead(
///         ICurrentUser caller,
///         string visibility,        // "public" | "private"
///         Guid?  ownerOrgId,        // null for public ownerless plugins
///         IReadOnlySet&lt;Guid&gt; callerOrgIds) // empty when anonymous or no memberships
///
///     AccessDecision DecideWrite(
///         ICurrentUser caller,
///         Guid         ownerOrgId,
///         IReadOnlySet&lt;Guid&gt; callerOrgIds)
///
///   sealed class AddOnAccessPolicy : IAddOnAccessPolicy
///     Constructed with new AddOnAccessPolicy() — no dependencies (pure)
///
/// Decision matrix (read/download, from design.md §5 + tasks.md §2.2):
///   public  + anonymous          → Allow
///   public  + authenticated      → Allow
///   private + anonymous          → Unauthenticated  (maps to 401)
///   private + auth non-member    → NotFound         (maps to 404, non-disclosure)
///   private + member             → Allow
///
/// Write decision (design.md §5 "Resource-Scoped AuthZ"):
///   private write + unauthenticated  → Unauthenticated (maps to 401)
///   private write + non-member       → Forbidden       (maps to 403)
///   private write + member           → Allow
/// </summary>
public sealed class PluginAccessPolicyTests
{
    // =========================================================================
    // Helpers — anonymous factory, no mutation (immutability rule)
    // =========================================================================

    private static ICurrentUser AnonymousUser()
    {
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.UserId.Returns((Guid?)null);
        user.IsAuthenticated.Returns(false);
        user.Email.Returns((string?)null);
        return user;
    }

    private static ICurrentUser AuthenticatedUser(Guid? userId = null)
    {
        Guid id = userId ?? Guid.NewGuid();
        ICurrentUser user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(id);
        user.IsAuthenticated.Returns(true);
        user.Email.Returns("user@example.com");
        return user;
    }

    private static IAddOnAccessPolicy MakePolicy() => new AddOnAccessPolicy();

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();

    // =========================================================================
    // READ MATRIX — public plugin
    // =========================================================================

    [Fact]
    public void DecideRead_PublicPlugin_AnonymousCaller_ReturnsAllow()
    {
        // Arrange
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AnonymousUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        // Act
        AccessDecision decision = policy.DecideRead(caller, "public", ownerOrgId: null, callerOrgIds);

        // Assert — public plugins are always readable, even anonymously
        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void DecideRead_PublicPlugin_AuthenticatedNonMember_ReturnsAllow()
    {
        // Arrange
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>(); // not in any org

        // Act
        AccessDecision decision = policy.DecideRead(caller, "public", ownerOrgId: null, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void DecideRead_PublicPlugin_AuthenticatedMember_ReturnsAllow()
    {
        // Arrange
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgA };

        // Act
        AccessDecision decision = policy.DecideRead(caller, "public", ownerOrgId: OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void DecideRead_PublicPlugin_WithOwnerOrgSet_AnonymousCaller_ReturnsAllow()
    {
        // Arrange — public plugin that also happens to have an owner org
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AnonymousUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        // Act
        AccessDecision decision = policy.DecideRead(caller, "public", ownerOrgId: OrgA, callerOrgIds);

        // Assert — visibility=public overrides org check
        Assert.Equal(AccessDecision.Allow, decision);
    }

    // =========================================================================
    // READ MATRIX — private plugin
    // =========================================================================

    [Fact]
    public void DecideRead_PrivatePlugin_AnonymousCaller_ReturnsUnauthenticated()
    {
        // Arrange
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AnonymousUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        // Act
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: OrgA, callerOrgIds);

        // Assert — private + anon → 401 (reveal nothing about the plugin's existence)
        Assert.Equal(AccessDecision.Unauthenticated, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_AuthenticatedNonMember_ReturnsNotFound()
    {
        // Arrange — caller is authenticated but not in owning org
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgB }; // member of OrgB, NOT OrgA

        // Act
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: OrgA, callerOrgIds);

        // Assert — non-disclosure rule: auth non-member sees 404, NOT 403
        Assert.Equal(AccessDecision.NotFound, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_AuthenticatedNonMember_NoOrgs_ReturnsNotFound()
    {
        // Arrange — caller is authenticated but has zero org memberships
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>(); // no org memberships

        // Act
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.NotFound, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_AuthenticatedMember_ReturnsAllow()
    {
        // Arrange — caller is authenticated AND is a member of the owning org
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgA }; // member of owning org

        // Act
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_MemberOfMultipleOrgs_OwningOrgIncluded_ReturnsAllow()
    {
        // Arrange — member of several orgs; owning org is in the set
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgB, OrgA }; // OrgA is owner

        // Act
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_MemberOfMultipleOrgs_OwningOrgNotIncluded_ReturnsNotFound()
    {
        // Arrange — member of several orgs but owning org is absent
        IAddOnAccessPolicy policy = MakePolicy();
        Guid orgC = Guid.NewGuid();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgB, orgC }; // OrgA missing

        // Act
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.NotFound, decision);
    }

    // =========================================================================
    // WRITE MATRIX — private write decisions
    // =========================================================================

    [Fact]
    public void DecideWrite_UnauthenticatedCaller_ReturnsUnauthenticated()
    {
        // Arrange
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AnonymousUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        // Act
        AccessDecision decision = policy.DecideWrite(caller, OrgA, callerOrgIds);

        // Assert — unauthenticated write → 401
        Assert.Equal(AccessDecision.Unauthenticated, decision);
    }

    [Fact]
    public void DecideWrite_AuthenticatedNonMember_ReturnsForbidden()
    {
        // Arrange — caller is authenticated but NOT in owning org
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgB }; // not OrgA

        // Act
        AccessDecision decision = policy.DecideWrite(caller, OrgA, callerOrgIds);

        // Assert — write by non-member → 403 (NOT 404; non-disclosure only for reads)
        Assert.Equal(AccessDecision.Forbidden, decision);
    }

    [Fact]
    public void DecideWrite_AuthenticatedNonMember_NoOrgs_ReturnsForbidden()
    {
        // Arrange — caller is authenticated but has no org memberships
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        // Act
        AccessDecision decision = policy.DecideWrite(caller, OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.Forbidden, decision);
    }

    [Fact]
    public void DecideWrite_AuthenticatedMember_ReturnsAllow()
    {
        // Arrange — caller is authenticated AND is in owning org
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgA }; // member of owning org

        // Act
        AccessDecision decision = policy.DecideWrite(caller, OrgA, callerOrgIds);

        // Assert
        Assert.Equal(AccessDecision.Allow, decision);
    }

    // =========================================================================
    // BOUNDARY / EDGE CASES
    // =========================================================================

    [Fact]
    public void DecideRead_EmptyCallerOrgIds_PrivatePlugin_AuthenticatedCaller_ReturnsNotFound()
    {
        // Edge case: authenticated but empty set (freshly registered user with no orgs)
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: Guid.NewGuid(), callerOrgIds);

        Assert.Equal(AccessDecision.NotFound, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_NullOwnerOrgId_AnonymousCaller_ReturnsUnauthenticated()
    {
        // Edge case: private plugin with null ownerOrgId (schema violation, but policy must handle it)
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AnonymousUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid>();

        // Private + null owner org + anonymous → still treat as private+anonymous → Unauthenticated
        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: null, callerOrgIds);

        Assert.Equal(AccessDecision.Unauthenticated, decision);
    }

    [Fact]
    public void DecideRead_PrivatePlugin_NullOwnerOrgId_AuthenticatedCaller_ReturnsNotFound()
    {
        // Edge case: private plugin with null ownerOrgId — authenticated caller cannot be member
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgA };

        AccessDecision decision = policy.DecideRead(caller, "private", ownerOrgId: null, callerOrgIds);

        Assert.Equal(AccessDecision.NotFound, decision);
    }

    [Theory]
    [InlineData("public")]
    public void DecideRead_PublicPlugin_LargeCallerOrgSet_ReturnsAllow(string visibility)
    {
        // Performance edge case: large org set must not affect correctness
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> manyOrgs = Enumerable.Range(0, 500)
            .Select(_ => Guid.NewGuid())
            .ToHashSet();

        AccessDecision decision = policy.DecideRead(caller, visibility, ownerOrgId: null, manyOrgs);

        Assert.Equal(AccessDecision.Allow, decision);
    }

    [Fact]
    public void DecideWrite_AuthenticatedMemberOfMultipleOrgs_ReturnsAllow()
    {
        // Member of multiple orgs — owning org is among them
        IAddOnAccessPolicy policy = MakePolicy();
        ICurrentUser caller = AuthenticatedUser();
        IReadOnlySet<Guid> callerOrgIds = new HashSet<Guid> { OrgB, OrgA };

        AccessDecision decision = policy.DecideWrite(caller, OrgA, callerOrgIds);

        Assert.Equal(AccessDecision.Allow, decision);
    }
}
