namespace ClaudeForge.Core.Modules.Identity.UseCases;

/// <summary>Result of initiating an OAuth/OIDC sign-in flow.</summary>
public sealed record InitiateSignInResult(
    string AuthorizationUrl,
    string State);

/// <summary>Token pair returned after a successful sign-in or token refresh.</summary>
public sealed record SignInTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Response body for GET /auth/me.
/// </summary>
public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<OrgMembershipSummary> OrgMemberships);

/// <summary>Summary of a single org membership for the current user.</summary>
public sealed record OrgMembershipSummary(
    Guid OrgId,
    string OrgName,
    string Role);

/// <summary>RFC 8628 device authorization response.</summary>
public sealed record DeviceCodeResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUrl,
    int ExpiresIn,
    int Interval);

/// <summary>Discriminated union for the device code approval result.</summary>
public abstract record ApproveDeviceCodeResult
{
    private ApproveDeviceCodeResult() { }

    /// <summary>User code was valid and tokens have been minted.</summary>
    public sealed record Success : ApproveDeviceCodeResult;

    /// <summary>User code is unknown or was null/empty.</summary>
    public sealed record NotFound : ApproveDeviceCodeResult;

    /// <summary>User code was found but the device code has expired.</summary>
    public sealed record Expired : ApproveDeviceCodeResult;

    /// <summary>User code has already been approved (single-use).</summary>
    public sealed record AlreadyApproved : ApproveDeviceCodeResult;
}

/// <summary>Discriminated union for the device token polling result.</summary>
public abstract record DeviceTokenPollResult
{
    private DeviceTokenPollResult() { }

    /// <summary>User has not yet approved the device request.</summary>
    public sealed record Pending : DeviceTokenPollResult;

    /// <summary>Client is polling too frequently — must back off.</summary>
    public sealed record SlowDown : DeviceTokenPollResult;

    /// <summary>User approved — tokens are included.</summary>
    public sealed record Approved(SignInTokens Tokens) : DeviceTokenPollResult;

    /// <summary>Device code has expired or is unknown.</summary>
    public sealed record Expired : DeviceTokenPollResult;
}
