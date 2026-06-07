using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClaudeForge.Api.Infrastructure.Context;
using ClaudeForge.Api.Module;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Modules.Identity.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Identity.Validation;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClaudeForge.Api.Modules.Identity;

/// <summary>
/// Feature module for Identity: JWKS endpoint, token issuer, refresh-token store,
/// JWT denylist services, auth flow state, use cases, and auth endpoints.
///
/// DESIGN NOTE: All IConfiguration reads are done lazily inside factory lambdas
/// (at DI resolution time, not at module registration time). This ensures that
/// WebApplicationFactory.ConfigureAppConfiguration overrides are always respected.
/// </summary>
public sealed class IdentityModule : IModule
{
    public IServiceCollection RegisterModule(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // ── ITokenIssuerPort ─────────────────────────────────────────────────────
        // Lazy: reads config at first resolution, AFTER test factory config overrides.
        services.AddSingleton<ITokenIssuerPort>(sp =>
        {
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            string? pem = cfg["Jwt:PrivatePem"];
            string iss = cfg["Jwt:Issuer"] ?? "https://claudeforge.io";
            string aud = cfg["Jwt:Audience"] ?? "claudeforge-api";
            string k = cfg["Jwt:Kid"] ?? "primary";
            int atm = int.TryParse(cfg["Jwt:AccessTokenMinutes"], out int a) ? a : 15;

            if (string.IsNullOrWhiteSpace(pem))
            {
                throw new InvalidOperationException(
                    "ITokenIssuerPort cannot be created: Jwt:PrivatePem is not configured. " +
                    "Set the JWT__SIGNINGKEY__PRIVATEPEM or Jwt:PrivatePem configuration value.");
            }

            return new RsaTokenIssuerAdapter(
                privatePem: pem,
                issuer: iss,
                audience: aud,
                accessTokenMinutes: atm,
                kid: k);
        });

        // ── IJwksProvider ────────────────────────────────────────────────────────
        // Lazy: picks up test factory's public key override if present.
        services.AddSingleton<IJwksProvider>(sp =>
        {
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            string? pubPem = cfg["Jwt:PublicPem"];
            string k = cfg["Jwt:Kid"] ?? "primary";
            if (!string.IsNullOrWhiteSpace(pubPem))
            {
                return new RsaJwksProvider([(pubPem, k)]);
            }
            return new RsaJwksProvider([]);
        });

        // ── IRefreshTokenStorePort ───────────────────────────────────────────────
        services.AddScoped<IRefreshTokenStorePort>(sp =>
        {
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            int rtd = int.TryParse(cfg["Jwt:RefreshTokenDays"], out int d) ? d : 30;
            return new RefreshTokenStoreAdapter(
                sp.GetRequiredService<MarketplaceDbContext>(),
                defaultExpiryDays: rtd);
        });

        // ── IRevokedJtiStorePort ─────────────────────────────────────────────────
        services.AddScoped<IRevokedJtiStorePort>(sp =>
            new PostgresRevokedJtiStoreAdapter(
                sp.GetRequiredService<MarketplaceDbContext>()));

        // ── Group 4 — OIDC Integration ───────────────────────────────────────────
        services.AddSingleton<IOpenIdConfigurationProvider>(sp =>
            new OidcConfigurationProvider(sp.GetRequiredService<IConfiguration>()));

        services.AddHttpClient("oidc-google");
        services.AddHttpClient("oidc-microsoft");

        services.AddSingleton<GoogleIdentityProviderAdapter>(sp =>
            new GoogleIdentityProviderAdapter(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("oidc-google"),
                sp.GetRequiredService<IOpenIdConfigurationProvider>()));

        services.AddSingleton<MicrosoftIdentityProviderAdapter>(sp =>
            new MicrosoftIdentityProviderAdapter(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("oidc-microsoft"),
                sp.GetRequiredService<IOpenIdConfigurationProvider>()));

        services.AddSingleton<IIdentityProviderPort>(sp =>
            sp.GetRequiredService<GoogleIdentityProviderAdapter>());
        services.AddSingleton<INamedIdentityProviderPort>(sp =>
            sp.GetRequiredService<GoogleIdentityProviderAdapter>());
        services.AddSingleton<IIdentityProviderPort>(sp =>
            sp.GetRequiredService<MicrosoftIdentityProviderAdapter>());
        services.AddSingleton<INamedIdentityProviderPort>(sp =>
            sp.GetRequiredService<MicrosoftIdentityProviderAdapter>());

        services.AddSingleton<IIdentityProviderRegistry>(sp =>
            new IdentityProviderRegistry(
                sp.GetRequiredService<IConfiguration>(),
                sp.GetServices<IIdentityProviderPort>()));

        services.Configure<UserStoreOptions>(configuration.GetSection("OIDC__USERSTORE"));

        // Register UserStoreAdapter using the scoped DbContextFactory which respects
        // any test overrides. The factory creates fresh contexts (which are then
        // properly tracked via the factory's own scope management).
        services.AddScoped<IUserStorePort>(sp =>
            new ScopedUserStoreAdapter(
                sp.GetRequiredService<MarketplaceDbContext>(),
                sp.GetRequiredService<IOptions<UserStoreOptions>>().Value));

        // ── Group 5 — Auth Flow State ────────────────────────────────────────────
        services.AddSingleton<IAuthFlowStatePort>(_ =>
            new InMemoryAuthFlowStateStore(TimeProvider.System));

        // ── Group 5 — Use Cases ──────────────────────────────────────────────────
        services.AddScoped<InitiateSignInUseCase>(sp =>
            new InitiateSignInUseCase(
                sp.GetRequiredService<IIdentityProviderRegistry>(),
                sp.GetRequiredService<IAuthFlowStatePort>()));

        services.AddScoped<CompleteSignInUseCase>(sp =>
        {
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            int rtd = int.TryParse(cfg["Jwt:RefreshTokenDays"], out int d) ? d : 30;
            return new CompleteSignInUseCase(
                sp.GetRequiredService<IIdentityProviderRegistry>(),
                sp.GetRequiredService<IAuthFlowStatePort>(),
                sp.GetRequiredService<IUserStorePort>(),
                sp.GetRequiredService<ITokenIssuerPort>(),
                sp.GetRequiredService<IRefreshTokenStorePort>(),
                rtd);
        });

        services.AddScoped<RefreshTokensUseCase>(sp =>
        {
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            int rtd = int.TryParse(cfg["Jwt:RefreshTokenDays"], out int d) ? d : 30;
            return new RefreshTokensUseCase(
                sp.GetRequiredService<IRefreshTokenStorePort>(),
                sp.GetRequiredService<ITokenIssuerPort>(),
                sp.GetRequiredService<IRevokedJtiStorePort>(),
                rtd);
        });

        services.AddScoped<GetCurrentUserUseCase>(sp =>
            new GetCurrentUserUseCase(
                sp.GetRequiredService<IUserStorePort>()));

        services.AddScoped<SignOutUseCase>(sp =>
            new SignOutUseCase(
                sp.GetRequiredService<IRefreshTokenStorePort>(),
                sp.GetRequiredService<IRevokedJtiStorePort>()));

        // Device code use cases (Singleton — DeviceCodeStore is a singleton in-memory store)
        services.AddSingleton<DeviceCodeStore>();
        services.AddSingleton<IssueDeviceCodeUseCase>(sp =>
        {
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            string iss = cfg["Jwt:Issuer"] ?? "https://claudeforge.io";
            return new IssueDeviceCodeUseCase(
                sp.GetRequiredService<IIdentityProviderRegistry>(),
                sp.GetRequiredService<DeviceCodeStore>(),
                iss);
        });
        services.AddSingleton<PollDeviceTokenUseCase>(sp =>
            new PollDeviceTokenUseCase(
                sp.GetRequiredService<DeviceCodeStore>()));

        // ── Group 5 — ICurrentUser (HttpContextCurrentUser) ─────────────────────
        // Use TryAdd to avoid duplicate registration if another module registers ICurrentUser.
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();

        // ── Group 5 — JwtBearer Authentication ──────────────────────────────────
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Disable claim type remapping so "sub" stays as "sub"
                options.MapInboundClaims = false;
            });

        // Post-configure reads issuer/audience/keys from DI at runtime.
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>,
            JwksProviderPostConfigureOptions>();

        // ── Group 5 — Authorization ──────────────────────────────────────────────
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy("RequireAuthenticatedUser",
                p => p.RequireAuthenticatedUser());
        });

        // ── OidcOptions validation ────────────────────────────────────────────────
        services.Configure<OidcOptions>(configuration.GetSection("OIDC"));

        bool isProduction = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Production",
            StringComparison.OrdinalIgnoreCase);

        services.AddSingleton<IValidateOptions<OidcOptions>>(
            _ => new OidcConfigValidator(isProduction));
        services.AddSingleton<IValidateOptions<JwtOptions>>(
            _ => new JwtSigningKeyValidator());
        services.Configure<JwtOptions>(configuration.GetSection("JWT"));

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // ── JWKS endpoint — public ───────────────────────────────────────────────
        endpoints.MapGet(
            "/.well-known/jwks.json",
            (IJwksProvider provider) =>
            {
                JwksDocument doc = provider.GetCurrentKeys();
                return Results.Ok(doc);
            })
            .AllowAnonymous()
            .WithName("GetJwks")
            .WithTags("Identity");

        // ── GET /auth/authorize?provider=&redirect_uri= ──────────────────────────
        endpoints.MapGet(
            "/auth/authorize",
            async (
                string? provider,
                string? redirect_uri,
                [FromServices] InitiateSignInUseCase useCase,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(provider))
                {
                    return Results.Problem(
                        detail: "The 'provider' query parameter is required.",
                        statusCode: 400);
                }

                InitiateSignInResult result = await useCase.ExecuteAsync(provider, redirect_uri, ct);
                return Results.Redirect(result.AuthorizationUrl);
            })
            .AllowAnonymous()
            .WithName("AuthorizeOidc")
            .WithTags("Identity");

        // ── GET /auth/callback?code=&state= ─────────────────────────────────────
        endpoints.MapGet(
            "/auth/callback",
            async (
                string? code,
                string? state,
                [FromServices] CompleteSignInUseCase useCase,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                {
                    return Results.Problem(
                        detail: "The 'code' and 'state' query parameters are required.",
                        statusCode: 400);
                }

                SignInTokens tokens = await useCase.ExecuteAsync(code, state, ct);

                return Results.Ok(new
                {
                    accessToken = tokens.AccessToken,
                    refreshToken = tokens.RefreshToken,
                    expiresAt = tokens.ExpiresAt,
                });
            })
            .AllowAnonymous()
            .WithName("OidcCallback")
            .WithTags("Identity");

        // ── POST /auth/token {code, state, codeVerifier} ────────────────────────
        endpoints.MapPost(
            "/auth/token",
            async (
                TokenRequest request,
                [FromServices] CompleteSignInUseCase useCase,
                CancellationToken ct) =>
            {
                SignInTokens tokens = await useCase.ExecuteAsync(request.Code, request.State, ct);

                return Results.Ok(new
                {
                    accessToken = tokens.AccessToken,
                    refreshToken = tokens.RefreshToken,
                    expiresAt = tokens.ExpiresAt,
                });
            })
            .AllowAnonymous()
            .WithName("ExchangeToken")
            .WithTags("Identity");

        // ── POST /auth/refresh {refreshToken} ────────────────────────────────────
        endpoints.MapPost(
            "/auth/refresh",
            async (
                RefreshRequest request,
                [FromServices] RefreshTokensUseCase useCase,
                CancellationToken ct) =>
            {
                SignInTokens tokens = await useCase.ExecuteAsync(request.RefreshToken, ct);

                return Results.Ok(new
                {
                    accessToken = tokens.AccessToken,
                    refreshToken = tokens.RefreshToken,
                    expiresAt = tokens.ExpiresAt,
                });
            })
            .AllowAnonymous()
            .WithName("RefreshTokens")
            .WithTags("Identity");

        // ── GET /auth/me [Authorize] ─────────────────────────────────────────────
        endpoints.MapGet(
            "/auth/me",
            async (
                [FromServices] ICurrentUser currentUser,
                [FromServices] GetCurrentUserUseCase useCase,
                CancellationToken ct) =>
            {
                if (!currentUser.IsAuthenticated || currentUser.UserId is null)
                {
                    return Results.Unauthorized();
                }

                CurrentUserResponse profile = await useCase.ExecuteAsync(currentUser.UserId.Value, ct);

                return Results.Ok(new
                {
                    userId = profile.UserId,
                    email = profile.Email,
                    displayName = profile.DisplayName,
                    orgMemberships = profile.OrgMemberships.Select(m => new
                    {
                        orgId = m.OrgId,
                        orgName = m.OrgName,
                        role = m.Role,
                    }),
                });
            })
            .RequireAuthorization("RequireAuthenticatedUser")
            .WithName("GetCurrentUser")
            .WithTags("Identity");

        // ── POST /auth/signout {refreshToken} [Authorize] ────────────────────────
        endpoints.MapPost(
            "/auth/signout",
            async (
                SignOutRequest request,
                ClaimsPrincipal principal,
                [FromServices] SignOutUseCase useCase,
                CancellationToken ct) =>
            {
                string? jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                string? expStr = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
                DateTimeOffset? expiry = null;
                if (long.TryParse(expStr, out long expEpoch))
                {
                    expiry = DateTimeOffset.FromUnixTimeSeconds(expEpoch);
                }

                await useCase.ExecuteAsync(request.RefreshToken, jti, expiry, ct);
                return Results.NoContent();
            })
            .RequireAuthorization("RequireAuthenticatedUser")
            .WithName("SignOut")
            .WithTags("Identity");

        // ── POST /auth/device/code {provider} ────────────────────────────────────
        endpoints.MapPost(
            "/auth/device/code",
            async (
                DeviceCodeRequest request,
                [FromServices] IssueDeviceCodeUseCase useCase,
                CancellationToken ct) =>
            {
                DeviceCodeResponse response = await useCase.ExecuteAsync(request.Provider, ct);

                return Results.Ok(new
                {
                    deviceCode = response.DeviceCode,
                    userCode = response.UserCode,
                    verificationUrl = response.VerificationUrl,
                    expiresIn = response.ExpiresIn,
                    interval = response.Interval,
                });
            })
            .AllowAnonymous()
            .WithName("IssueDeviceCode")
            .WithTags("Identity");

        // ── POST /auth/device/token {deviceCode} ─────────────────────────────────
        endpoints.MapPost(
            "/auth/device/token",
            async (
                DeviceTokenRequest request,
                [FromServices] PollDeviceTokenUseCase useCase,
                CancellationToken ct) =>
            {
                DeviceTokenPollResult result = await useCase.ExecuteAsync(request.DeviceCode, ct);

                return result switch
                {
                    DeviceTokenPollResult.Approved approved => Results.Ok(new
                    {
                        accessToken = approved.Tokens.AccessToken,
                        refreshToken = approved.Tokens.RefreshToken,
                        expiresAt = approved.Tokens.ExpiresAt,
                    }),
                    DeviceTokenPollResult.Pending => Results.Ok(new
                    {
                        error = "authorization_pending",
                        status = "pending",
                    }),
                    DeviceTokenPollResult.SlowDown => Results.Ok(new
                    {
                        error = "slow_down",
                        status = "slow_down",
                    }),
                    DeviceTokenPollResult.Expired => Results.Problem(
                        detail: "Device code has expired or is not recognized.",
                        statusCode: 400),
                    _ => Results.Problem(statusCode: 500),
                };
            })
            .AllowAnonymous()
            .WithName("PollDeviceToken")
            .WithTags("Identity");

        return endpoints;
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────────

    private sealed record TokenRequest(string Code, string State, string CodeVerifier);
    private sealed record RefreshRequest(string RefreshToken);
    private sealed record SignOutRequest(string RefreshToken);
    private sealed record DeviceCodeRequest(string Provider);
    private sealed record DeviceTokenRequest(string DeviceCode);
}

/// <summary>
/// Simplified <see cref="IUserStorePort"/> adapter that uses the scoped
/// <see cref="MarketplaceDbContext"/> directly (without creating a new scope per call).
/// This correctly picks up the test WebApplicationFactory's DbContext override.
/// The scoped context is NOT disposed by this adapter (DI manages its lifetime).
/// </summary>
internal sealed class ScopedUserStoreAdapter : IUserStorePort
{
    private readonly MarketplaceDbContext _db;
    private readonly UserStoreOptions _options;

    public ScopedUserStoreAdapter(MarketplaceDbContext db, UserStoreOptions options)
    {
        _db = db;
        _options = options;
    }

    public async Task<ProvisionedUser> ProvisionOrLinkAsync(
        string provider, string subject, string email,
        bool emailVerified, string displayName, CancellationToken ct = default)
    {
        string emailNormalized = email.ToLowerInvariant();

        // Rule 1: Existing (provider, subject) → update
        UserIdentityEntity? existingIdentity = await _db.UserIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Provider == provider && i.Subject == subject, ct)
            .ConfigureAwait(false);

        if (existingIdentity is not null)
        {
            UserEntity? user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == existingIdentity.UserId, ct)
                .ConfigureAwait(false);

            if (user is not null)
            {
                user.Email = email;
                user.EmailNormalized = emailNormalized;
                user.DisplayName = displayName;
                user.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                return new ProvisionedUser(user.Id, user.Email, user.DisplayName, false);
            }
        }

        // Rule 2: Cross-provider linking via verified email
        if (emailVerified && !_options.DisableCrossProviderLinking)
        {
            UserEntity? existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct)
                .ConfigureAwait(false);

            if (existingUser is not null)
            {
                UserIdentityEntity newIdentity = new()
                {
                    Id = Guid.NewGuid(),
                    UserId = existingUser.Id,
                    Provider = provider,
                    Subject = subject,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                _db.UserIdentities.Add(newIdentity);
                try
                {
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return new ProvisionedUser(existingUser.Id, existingUser.Email, existingUser.DisplayName, false);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException)
                {
                    _db.ChangeTracker.Clear();
                }
            }
        }

        // Rule 3: Create new user + identity
        DateTimeOffset now = DateTimeOffset.UtcNow;
        UserEntity newUser = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            EmailNormalized = emailNormalized,
            DisplayName = displayName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        UserIdentityEntity identity = new()
        {
            Id = Guid.NewGuid(),
            UserId = newUser.Id,
            Provider = provider,
            Subject = subject,
            CreatedAt = now,
        };
        _db.Users.Add(newUser);
        _db.UserIdentities.Add(identity);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return new ProvisionedUser(newUser.Id, newUser.Email, newUser.DisplayName, true);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            // Concurrent insert — fetch winner
            UserIdentityEntity winner = await _db.UserIdentities
                .AsNoTracking()
                .Include(i => i.User)
                .FirstAsync(i => i.Provider == provider && i.Subject == subject, ct)
                .ConfigureAwait(false);
            return new ProvisionedUser(winner.UserId, winner.User.Email, winner.User.DisplayName, false);
        }
    }

    public async Task<UserProfile?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                Memberships = u.Memberships
                    .Select(m => new { m.OrgId, OrgName = m.Organization.Name, m.Role })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            return null;
        }

        return new UserProfile(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Memberships
                .Select(m => new UserOrgMembership(m.OrgId, m.OrgName, m.Role))
                .ToList());
    }
}

/// <summary>
/// Post-configures <see cref="JwtBearerOptions"/> to inject the issuer, audience, and
/// signing key resolver from the DI-registered services at runtime. This allows the test
/// WebApplicationFactory to override both <see cref="IConfiguration"/> and
/// <see cref="IJwksProvider"/> and have JwtBearer validate test-issued tokens correctly.
/// </summary>
internal sealed class JwksProviderPostConfigureOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly IJwksProvider _jwksProvider;
    private readonly IConfiguration _configuration;

    public JwksProviderPostConfigureOptions(
        IJwksProvider jwksProvider,
        IConfiguration configuration)
    {
        _jwksProvider = jwksProvider;
        _configuration = configuration;
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        string issuer = _configuration["Jwt:Issuer"] ?? "https://claudeforge.io";
        string audience = _configuration["Jwt:Audience"] ?? "claudeforge-api";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                JwksDocument doc = _jwksProvider.GetCurrentKeys();

                List<SecurityKey> keys = new();
                foreach (JwksKey key in doc.Keys)
                {
                    try
                    {
                        System.Security.Cryptography.RSA rsa =
                            System.Security.Cryptography.RSA.Create();

                        System.Security.Cryptography.RSAParameters rsaParams = new()
                        {
                            Modulus = Base64UrlDecodeBytes(key.N),
                            Exponent = Base64UrlDecodeBytes(key.E),
                        };

                        rsa.ImportParameters(rsaParams);
                        keys.Add(new RsaSecurityKey(rsa) { KeyId = key.Kid });
                    }
                    catch
                    {
                        // Skip malformed keys gracefully.
                    }
                }

                return keys;
            },
        };

        options.Challenge = string.Empty;
    }

    private static byte[] Base64UrlDecodeBytes(string base64Url)
    {
        string padded = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
