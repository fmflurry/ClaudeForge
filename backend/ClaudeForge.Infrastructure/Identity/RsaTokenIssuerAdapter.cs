using System.Security.Claims;
using System.Security.Cryptography;
using ClaudeForge.Core.Identity.Ports;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// RS256 JWT access-token issuer and validator.
/// Implements <see cref="ITokenIssuerPort"/> using an RSA private key PEM.
/// </summary>
public sealed class RsaTokenIssuerAdapter : ITokenIssuerPort
{
    private readonly RsaSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly string _kid;
    private readonly TokenValidationParameters _validationParameters;

    /// <param name="privatePem">RSA private key in PEM format (PKCS#8 or traditional).</param>
    /// <param name="issuer">JWT "iss" claim value.</param>
    /// <param name="audience">JWT "aud" claim value.</param>
    /// <param name="accessTokenMinutes">Token lifetime in minutes. Default 15.</param>
    /// <param name="kid">Key identifier placed in the JWT header.</param>
    public RsaTokenIssuerAdapter(
        string privatePem,
        string issuer,
        string audience,
        int accessTokenMinutes = 15,
        string kid = "primary")
    {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(privatePem);

        _signingKey = new RsaSecurityKey(rsa) { KeyId = kid };
        _issuer = issuer;
        _audience = audience;
        _accessTokenMinutes = accessTokenMinutes;
        _kid = kid;

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = _signingKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuerSigningKey = true,
        };
    }

    /// <inheritdoc />
    public string IssueAccessToken(AccessTokenClaims claims)
    {
        // Truncate to seconds to avoid sub-second rounding that could cause
        // jwt.IssuedAt (which is read back as epoch-seconds) to differ from now.
        long nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long expEpoch = nowEpoch + ((long)_accessTokenMinutes * 60);

        // Explicitly include iat as a numeric claim so that JwtSecurityToken.IssuedAt
        // (which reads the "iat" claim) returns the correct value.  Without this the
        // JwtSecurityToken constructor does NOT auto-populate iat, leaving IssuedAt as
        // DateTime.MinValue and making (ValidTo - IssuedAt) astronomically large.
        Claim[] tokenClaims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, claims.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, claims.Email),
            new Claim(JwtRegisteredClaimNames.Name, claims.Name),
            new Claim("provider", claims.Provider),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, nowEpoch.ToString(), ClaimValueTypes.Integer64),
        ];

        SigningCredentials signingCredentials = new(
            _signingKey,
            SecurityAlgorithms.RsaSha256);

        // Convert epoch seconds back to UTC DateTime for the constructor.
        DateTime notBeforeUtc = DateTimeOffset.FromUnixTimeSeconds(nowEpoch).UtcDateTime;
        DateTime expiresUtc = DateTimeOffset.FromUnixTimeSeconds(expEpoch).UtcDateTime;

        // When accessTokenMinutes is negative the token is intentionally already-expired.
        // The JwtSecurityToken constructor rejects expires < notBefore with ArgumentException,
        // so omit notBefore in that case so the constructor accepts it and the validator then
        // throws SecurityTokenExpiredException as the tests require.
        JwtSecurityToken jwt = new(
            issuer: _issuer,
            audience: _audience,
            claims: tokenClaims,
            notBefore: expiresUtc >= notBeforeUtc ? notBeforeUtc : null,
            expires: expiresUtc,
            signingCredentials: signingCredentials);

        // Ensure the kid header is set
        jwt.Header[JwtHeaderParameterNames.Kid] = _kid;

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    /// <inheritdoc />
    public ClaimsPrincipal ValidateAccessToken(string rawToken)
    {
        JwtSecurityTokenHandler handler = new()
        {
            // Disable the default inbound claim-type map so claim names such as "sub"
            // are not silently remapped to ClaimTypes.NameIdentifier etc.
            MapInboundClaims = false,
        };

        try
        {
            ClaimsPrincipal principal = handler.ValidateToken(
                rawToken,
                _validationParameters,
                out _);
            return principal;
        }
        catch (SecurityTokenException)
        {
            // Re-throw as-is — these are already the expected exception types.
            throw;
        }
        catch (ArgumentException ex) when (ex.InnerException is FormatException)
        {
            // When the payload contains invalid base-64 characters (e.g. a tampered token)
            // JwtSecurityTokenHandler.ValidateToken propagates an ArgumentException whose
            // InnerException is a FormatException.  Wrap it so callers always receive a
            // SecurityTokenException as the ITokenIssuerPort contract requires.
            throw new SecurityTokenException("Token payload is malformed.", ex);
        }
        catch (FormatException ex)
        {
            // Defensive catch: some library versions surface the FormatException directly.
            throw new SecurityTokenException("Token payload is malformed.", ex);
        }
    }
}
