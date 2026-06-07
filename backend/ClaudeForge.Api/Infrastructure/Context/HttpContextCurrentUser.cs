using ClaudeForge.Core.Shared.Authorization;

namespace ClaudeForge.Api.Infrastructure.Context;

/// <summary>
/// <see cref="ICurrentUser"/> implementation for production use.
/// Reads the caller's identity from the HTTP context's ClaimsPrincipal.
/// UserId is read from the "sub" claim, Email from the "email" claim.
/// IsAuthenticated reflects the principal's authenticated state.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated
    {
        get
        {
            System.Security.Claims.ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
            return principal?.Identity?.IsAuthenticated ?? false;
        }
    }

    public Guid? UserId
    {
        get
        {
            string? sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            if (Guid.TryParse(sub, out Guid parsed))
                return parsed;
            return null;
        }
    }

    public string? Email
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value;
        }
    }
}
