using System.Security.Claims;
using ClaudeForge.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.Api;

/// <summary>
/// Unit tests for <see cref="HttpContextCurrentUser"/>:
///   - Anonymous (no principal / unauthenticated)
///   - Authenticated with sub + email claims
///   - Invalid sub claim (non-GUID)
///   - Missing sub claim
///   - Missing email claim
///   - Null HTTP context
/// </summary>
public sealed class HttpContextCurrentUserTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IHttpContextAccessor MakeAccessor(ClaimsPrincipal? principal)
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();

        if (principal is null)
        {
            accessor.HttpContext.Returns((HttpContext?)null);
        }
        else
        {
            DefaultHttpContext ctx = new() { User = principal };
            accessor.HttpContext.Returns(ctx);
        }

        return accessor;
    }

    private static IHttpContextAccessor MakeAccessorWithClaims(
        bool isAuthenticated,
        string? sub = null,
        string? email = null)
    {
        List<Claim> claims = new();
        if (sub is not null) claims.Add(new Claim("sub", sub));
        if (email is not null) claims.Add(new Claim("email", email));

        ClaimsIdentity identity = new(
            claims,
            authenticationType: isAuthenticated ? "test" : null);

        ClaimsPrincipal principal = new(identity);
        return MakeAccessor(principal);
    }

    // -----------------------------------------------------------------------
    // IsAuthenticated
    // -----------------------------------------------------------------------

    [Fact]
    public void IsAuthenticated_NullHttpContext_ReturnsFalse()
    {
        HttpContextCurrentUser user = new(MakeAccessor(principal: null));

        Assert.False(user.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_AnonymousPrincipal_ReturnsFalse()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(isAuthenticated: false);
        HttpContextCurrentUser user = new(accessor);

        Assert.False(user.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_AuthenticatedPrincipal_ReturnsTrue()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: Guid.NewGuid().ToString(),
            email: "alice@example.com");
        HttpContextCurrentUser user = new(accessor);

        Assert.True(user.IsAuthenticated);
    }

    // -----------------------------------------------------------------------
    // UserId
    // -----------------------------------------------------------------------

    [Fact]
    public void UserId_NullHttpContext_ReturnsNull()
    {
        HttpContextCurrentUser user = new(MakeAccessor(principal: null));

        Assert.Null(user.UserId);
    }

    [Fact]
    public void UserId_AnonymousPrincipal_ReturnsNull()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(isAuthenticated: false);
        HttpContextCurrentUser user = new(accessor);

        Assert.Null(user.UserId);
    }

    [Fact]
    public void UserId_ValidGuidSub_ReturnsCorrectGuid()
    {
        Guid expected = Guid.NewGuid();
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: expected.ToString());
        HttpContextCurrentUser user = new(accessor);

        Assert.Equal(expected, user.UserId);
    }

    [Fact]
    public void UserId_MissingSub_ReturnsNull()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: null,
            email: "alice@example.com");
        HttpContextCurrentUser user = new(accessor);

        Assert.Null(user.UserId);
    }

    [Fact]
    public void UserId_InvalidGuidSub_ReturnsNull()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: "not-a-guid",
            email: "alice@example.com");
        HttpContextCurrentUser user = new(accessor);

        Assert.Null(user.UserId);
    }

    // -----------------------------------------------------------------------
    // Email
    // -----------------------------------------------------------------------

    [Fact]
    public void Email_NullHttpContext_ReturnsNull()
    {
        HttpContextCurrentUser user = new(MakeAccessor(principal: null));

        Assert.Null(user.Email);
    }

    [Fact]
    public void Email_AnonymousPrincipal_ReturnsNull()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(isAuthenticated: false);
        HttpContextCurrentUser user = new(accessor);

        Assert.Null(user.Email);
    }

    [Fact]
    public void Email_AuthenticatedWithEmailClaim_ReturnsEmail()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: Guid.NewGuid().ToString(),
            email: "bob@example.com");
        HttpContextCurrentUser user = new(accessor);

        Assert.Equal("bob@example.com", user.Email);
    }

    [Fact]
    public void Email_MissingEmailClaim_ReturnsNull()
    {
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: Guid.NewGuid().ToString(),
            email: null);
        HttpContextCurrentUser user = new(accessor);

        Assert.Null(user.Email);
    }

    // -----------------------------------------------------------------------
    // Compound: all properties on an authenticated user
    // -----------------------------------------------------------------------

    [Fact]
    public void AllProperties_AuthenticatedUser_AllPopulatedCorrectly()
    {
        Guid userId = Guid.NewGuid();
        IHttpContextAccessor accessor = MakeAccessorWithClaims(
            isAuthenticated: true,
            sub: userId.ToString(),
            email: "carol@example.com");
        HttpContextCurrentUser user = new(accessor);

        Assert.True(user.IsAuthenticated);
        Assert.Equal(userId, user.UserId);
        Assert.Equal("carol@example.com", user.Email);
    }
}
