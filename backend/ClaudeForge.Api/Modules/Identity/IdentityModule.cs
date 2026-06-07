using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using ClaudeForge.Api.Infrastructure.Context;
using ClaudeForge.Api.Module;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Core.Modules.Identity.UseCases;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Identity;
using ClaudeForge.Infrastructure.Identity.Validation;
using ClaudeForge.Infrastructure.Organizations;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
    private const string AuthorizeRateLimitPolicy = "auth-authorize-limit";
    private const string TokenRateLimitPolicy = "auth-token-limit";
    private const string RefreshRateLimitPolicy = "auth-refresh-limit";
    private const string DeviceTokenRateLimitPolicy = "auth-device-token-limit";

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

        // Use a single UserStoreAdapter backed by the scoped MarketplaceDbContext.
        // This correctly picks up the WebApplicationFactory's DbContext override in tests.
        services.AddScoped<IUserStorePort>(sp =>
            new UserStoreAdapter(
                sp.GetRequiredService<MarketplaceDbContext>(),
                sp.GetRequiredService<IOptions<UserStoreOptions>>()));

        // ── Group 5 — Auth Flow State ────────────────────────────────────────────
        services.AddSingleton<IAuthFlowStatePort>(_ =>
            new InMemoryAuthFlowStateStore(TimeProvider.System));

        // ── Group 5 — Use Cases ──────────────────────────────────────────────────
        services.AddScoped<InitiateSignInUseCase>(sp =>
        {
            // C4: Pass the server-configured redirect URI and optional loopback URI so that
            // InitiateSignInUseCase can validate any caller-supplied redirect_uri against
            // the allow-list rather than accepting it verbatim.
            IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
            // The registered redirect URI for the default provider (google when enabled).
            string configuredRedirect = cfg["OIDC__GOOGLE__REDIRECTURI"]
                ?? cfg["OIDC__MICROSOFT__REDIRECTURI"]
                ?? string.Empty;
            string? loopbackRedirect = cfg["OIDC__ALLOWEDLOOPBACKREDIRECT"];
            return new InitiateSignInUseCase(
                sp.GetRequiredService<IIdentityProviderRegistry>(),
                sp.GetRequiredService<IAuthFlowStatePort>(),
                configuredRedirectUri: configuredRedirect,
                allowedLoopbackRedirect: loopbackRedirect);
        });

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
                sp.GetRequiredService<IUserStorePort>(),
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

        // ── Group 13 — GDPR Account Deletion ─────────────────────────────────────
        services.AddScoped<IUserDeletionPort>(sp =>
            new UserDeletionAdapter(
                sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<IOrgDeletionPort>(sp =>
            new OrgDeletionAdapter(
                sp.GetRequiredService<MarketplaceDbContext>()));

        services.AddScoped<DeleteAccountUseCase>(sp =>
            new DeleteAccountUseCase(
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<IUserDeletionPort>(),
                sp.GetRequiredService<IMembershipStorePort>(),
                sp.GetRequiredService<IRefreshTokenStorePort>(),
                sp.GetRequiredService<IOrgMembershipQueryPort>(),
                sp.GetRequiredService<IOrgDeletionPort>()));

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

        // ── Group 8 — Per-IP rate limiting for auth endpoints ────────────────────
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthorizeRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(TokenRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RefreshRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(DeviceTokenRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));
        });

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
            .WithTags("Identity")
            .RequireRateLimiting(AuthorizeRateLimitPolicy);

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
            .WithTags("Identity")
            .RequireRateLimiting(TokenRateLimitPolicy);

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
            .WithTags("Identity")
            .RequireRateLimiting(RefreshRateLimitPolicy);

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

        // ── DELETE /auth/me [Authorize] — GDPR account deletion ─────────────────
        endpoints.MapDelete(
            "/auth/me",
            async (
                [FromServices] DeleteAccountUseCase useCase,
                CancellationToken ct) =>
            {
                await useCase.ExecuteAsync(ct);
                return Results.NoContent();
            })
            .RequireAuthorization("RequireAuthenticatedUser")
            .WithName("DeleteAccount")
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
            .WithTags("Identity")
            .RequireRateLimiting(DeviceTokenRateLimitPolicy);

        return endpoints;
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────────

    private sealed record TokenRequest(string Code, string State, string CodeVerifier);
    private sealed record RefreshRequest(string RefreshToken);
    private sealed record SignOutRequest(string RefreshToken);
    private sealed record DeviceCodeRequest(string Provider);
    private sealed record DeviceTokenRequest(string DeviceCode);
}

