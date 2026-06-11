using System.Text.Json;
using System.Threading.RateLimiting;
using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.SecurityAnalysis.UseCases;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.SecurityAnalysis.Ports;
using ClaudeForge.Core.Modules.SecurityAnalysis.Services;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using ClaudeForge.Infrastructure.Security;
using ClaudeForge.Infrastructure.Security.DynamicAnalysis;
using ClaudeForge.Infrastructure.Security.Queue;
using ClaudeForge.Infrastructure.Security.Reputation;
using ClaudeForge.Infrastructure.Security.StaticAnalysis;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Api.Modules.SecurityAnalysis;

/// <summary>
/// Feature module for CLI Plugin Marketplace security analysis.
///
/// Route summary (Phase 2 — pipeline stubs, analysis worker starting):
///   POST   /api/v1/plugins/submit                                    — Submit plugin for analysis
///   GET    /api/v1/plugins/{pluginId:guid}/analysis                   — Get analysis results
///   POST   /api/v1/plugins/{pluginId:guid}/appeal                     — Submit appeal
///   GET    /api/v1/plugins/{pluginId:guid}/appeal                     — Get appeal status
///   POST   /api/v1/safe-zone/{orgId:guid}/plugins/{pluginId:guid}/approve  — Approve plugin for org safe zone
///   GET    /api/v1/safe-zone/{orgId:guid}/plugins                     — List org-safe plugins
///   GET    /api/v1/control-center/metrics                             — Admin metrics
///   GET    /api/v1/reputation/leaderboard                             — Author leaderboard
///   GET    /api/v1/reputation/authors/{authorId}                      — Author reputation detail
///   GET    /api/v1/reputation/badges                                  — List available badges
/// </summary>
public sealed class SecurityAnalysisModule : IModule
{
    private const string IpRateLimitPolicy = "security-analysis-ip";
    private const string AuthorRateLimitPolicy = "security-analysis-author";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // ═══════════════════════════════════════════════════════════════════
        // NOTE: CORS, ProblemDetails, JWT auth, rate limiting (Phase 1.2)
        // ═══════════════════════════════════════════════════════════════════

        // ── Rate limiting (Task 1.2.2) ─────────────────────────────────────
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(IpRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(AuthorRateLimitPolicy, httpContext =>
            {
                ICurrentUser? currentUser = httpContext.RequestServices.GetService<ICurrentUser>();
                string key = currentUser?.UserId?.ToString()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromDays(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });
        });

        // ═══════════════════════════════════════════════════════════════════
        // Phase 2: Analysis Pipeline Service Registration
        // ═══════════════════════════════════════════════════════════════════

        // ── 2.1 Static Analysis Adapters ───────────────────────────────────
        services.AddSingleton<IStaticAnalyzer, EslintAnalyzer>();
        services.AddSingleton<IStaticAnalyzer, SemgrepAnalyzer>();
        services.AddSingleton<IStaticAnalyzer, GitleaksAnalyzer>();
        services.AddSingleton<IStaticAnalyzer, TrivyAnalyzer>();

        // ── 2.2 Dynamic Analysis Adapter ───────────────────────────────────
        services.AddSingleton<IDynamicAnalyzer, DockerSandboxAnalyzer>();

        // ── 2.3 Scoring Engine + Decision Engine (singletons — stateless) ──
        services.AddSingleton<ScoringEngine>();
        services.AddSingleton<DecisionEngine>();

        // ── 2.4 + 2.5 Use Cases (scoped — depend on scoped ports) ─────────
        services.AddScoped<RunStaticAnalysisUseCase>();
        services.AddScoped<RunDynamicAnalysisUseCase>();
        services.AddScoped<SaveAnalysisResultUseCase>();

        // ── Infrastructure Adapters ────────────────────────────────────────
        services.AddScoped<ISaveAnalysisResultPort, SaveAnalysisResultAdapter>();
        services.AddScoped<IAnalysisQueue, AnalysisQueue>();

        // ── 2.5 Worker Hosted Service ──────────────────────────────────────
        services.AddHostedService<AnalysisWorkerHostedService>();

        // ═══════════════════════════════════════════════════════════════════
        // Phase 3: Safe Zone Service Registration
        // ═══════════════════════════════════════════════════════════════════

        // ── Port → Adapter ─────────────────────────────────────────────────
        services.AddScoped<ISafeZoneStorePort, SafeZoneStoreAdapter>();

        // ── Use Cases ──────────────────────────────────────────────────────
        services.AddScoped<ApproveAddOnForOrgUseCase>();
        services.AddScoped<ListSafeZoneAddOnsUseCase>();
        services.AddScoped<ListPendingSafeZoneAddOnsUseCase>();

        // ═══════════════════════════════════════════════════════════════════
        // Phase 5: Gamification Service Registration
        // ═══════════════════════════════════════════════════════════════════

        // ── 5.1 Karma System ────────────────────────────────────────────────
        // NOTE: IKarmaServicePort, IBadgeServicePort, ILeaderboardPort are
        // registered as scoped because they depend on MarketplaceDbContext.
        services.AddScoped<IKarmaServicePort, KarmaServiceAdapter>();
        services.AddScoped<AddKarmaUseCase>();

        // ── 5.2 Badges System ────────────────────────────────────────────────
        services.AddScoped<IBadgeServicePort, BadgeServiceAdapter>();
        services.AddScoped<AwardBadgesUseCase>();

        // ── 5.3 Leaderboard ──────────────────────────────────────────────────
        services.AddScoped<ILeaderboardPort, LeaderboardAdapter>();

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // ── Plugin Analysis ────────────────────────────────────────────────
        endpoints.MapPost("/api/v1/plugins/submit", (Delegate)SubmitAddOnHandler)
            .WithName("SubmitPluginForAnalysis")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .RequireRateLimiting(AuthorRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}/analysis", (Delegate)GetAnalysisHandler)
            .WithName("GetPluginAnalysis")
            .WithTags("SecurityAnalysis");

        endpoints.MapPost("/api/v1/plugins/{pluginId:guid}/appeal", (Delegate)SubmitAppealHandler)
            .WithName("SubmitPluginAppeal")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .RequireRateLimiting(AuthorRateLimitPolicy)
            .DisableAntiforgery();

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}/appeal", (Delegate)GetAppealStatusHandler)
            .WithName("GetPluginAppealStatus")
            .WithTags("SecurityAnalysis");

        // ── Safe Zone ──────────────────────────────────────────────────────
        endpoints.MapPost("/api/v1/safe-zone/{orgId:guid}/plugins/{pluginId:guid}/approve", (Delegate)ApprovePluginHandler)
            .WithName("ApprovePluginForSafeZone")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        endpoints.MapGet("/api/v1/safe-zone/{orgId:guid}/plugins", (Delegate)ListSafeZonePluginsHandler)
            .WithName("ListOrgSafePlugins")
            .WithTags("SecurityAnalysis");

        // ── Safe Zone — Pending Queue (3.2.5) ──────────────────────────────
        endpoints.MapGet("/api/v1/safe-zone/{orgId:guid}/pending", (Delegate)ListPendingSafeZoneAddOnsHandler)
            .WithName("ListPendingSafeZonePlugins")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        // ── Safe Zone — Global (3.3.4) ─────────────────────────────────────
        endpoints.MapGet("/api/v1/safe-zone/global", (Delegate)ListGlobalSafeZonePluginsHandler)
            .WithName("ListGlobalSafeZonePlugins")
            .WithTags("SecurityAnalysis");

        // ── Safe Zone — Org-level blocks (3.3.5) ──────────────────────────
        endpoints.MapPost("/api/v1/safe-zone/{orgId:guid}/plugins/{pluginId:guid}/block", (Delegate)BlockGlobalPluginHandler)
            .WithName("BlockGlobalPluginForOrg")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        endpoints.MapPost("/api/v1/safe-zone/{orgId:guid}/plugins/{pluginId:guid}/unblock", (Delegate)UnblockGlobalPluginHandler)
            .WithName("UnblockGlobalPluginForOrg")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        // ── Safe Zone — Request approval (3.4.4) ──────────────────────────
        endpoints.MapPost("/api/v1/safe-zone/{orgId:guid}/requests", (Delegate)RequestSafeZoneApprovalHandler)
            .WithName("RequestSafeZoneApproval")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        // ═══════════════════════════════════════════════════════════════════
        // Phase 4: Control Center Endpoints
        // ═══════════════════════════════════════════════════════════════════

        // A4: Control center metrics
        endpoints.MapGet("/api/v1/control-center/metrics", (Delegate)GetAdminMetricsHandler)
            .WithName("GetControlCenterMetrics")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        // A5: Appeals management
        endpoints.MapGet("/api/v1/control-center/appeals", (Delegate)ListAppealsHandler)
            .WithName("ListControlCenterAppeals")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        endpoints.MapGet("/api/v1/control-center/appeals/{appealId:guid}", (Delegate)GetAppealDetailHandler)
            .WithName("GetControlCenterAppealDetail")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        endpoints.MapPut("/api/v1/control-center/appeals/{appealId:guid}", (Delegate)ResolveAppealHandler)
            .WithName("ResolveControlCenterAppeal")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        // A6: Configuration
        endpoints.MapGet("/api/v1/control-center/config/analysis", (Delegate)GetAnalysisConfigHandler)
            .WithName("GetAnalysisConfig")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        endpoints.MapPut("/api/v1/control-center/config/analysis", (Delegate)UpdateAnalysisConfigHandler)
            .WithName("UpdateAnalysisConfig")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        endpoints.MapGet("/api/v1/control-center/config/history", (Delegate)GetConfigHistoryHandler)
            .WithName("GetAnalysisConfigHistory")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        // A7: Audit log
        endpoints.MapGet("/api/v1/control-center/audit-logs", (Delegate)GetAuditLogsHandler)
            .WithName("GetAuditLogs")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        // A8: Notifications
        endpoints.MapGet("/api/v1/notifications", (Delegate)GetNotificationsHandler)
            .WithName("GetNotifications")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization();

        endpoints.MapPut("/api/v1/notifications/{notificationId:guid}/read", (Delegate)MarkNotificationReadHandler)
            .WithName("MarkNotificationRead")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        endpoints.MapPut("/api/v1/notifications/read-all", (Delegate)MarkAllNotificationsReadHandler)
            .WithName("MarkAllNotificationsRead")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        endpoints.MapPut("/api/v1/notifications/preferences", (Delegate)UpdateNotificationPreferencesHandler)
            .WithName("UpdateNotificationPreferences")
            .WithTags("SecurityAnalysis")
            .RequireAuthorization()
            .DisableAntiforgery();

        // ── Reputation System ──────────────────────────────────────────────
        endpoints.MapGet("/api/v1/reputation/leaderboard", (Delegate)GetReputationLeaderboardHandler)
            .WithName("GetReputationLeaderboard")
            .WithTags("SecurityAnalysis");

        endpoints.MapGet("/api/v1/reputation/authors/{authorId}", (Delegate)GetAuthorReputationHandler)
            .WithName("GetAuthorReputation")
            .WithTags("SecurityAnalysis");

        endpoints.MapGet("/api/v1/reputation/badges", (Delegate)GetBadgesHandler)
            .WithName("GetAvailableBadges")
            .WithTags("SecurityAnalysis");

        return endpoints;
    }

    // =========================================================================
    // DTOs
    // =========================================================================

    private sealed record SubmitPluginRequest(Guid PluginId, string? Version);
    private sealed record ApprovePluginRequest(string? PluginVersion);
    private sealed record RequestApprovalBody(Guid PluginId, string? PluginVersion);
    private sealed record SubmitAppealBody(string Reason, Guid? FindingId, string? Evidence);
    private sealed record ResolveAppealBody(string Resolution, string? Notes);
    private sealed record UpdateAnalysisConfigBody(
        decimal? StaticWeight,
        decimal? DynamicWeight,
        decimal? PassThreshold,
        decimal? FailThreshold,
        int? MaxWorkers,
        int? RetryLimit,
        int? AnalysisTimeoutSeconds);
    private sealed record UpdateNotificationPreferencesBody(bool? EmailAlerts, bool? InAppAlerts);

    // =========================================================================
    // Handlers — Phase 2 & 3 existing
    // =========================================================================

    private static async Task<IResult> SubmitAddOnHandler(HttpContext httpContext)
    {
        SubmitPluginRequest? request;
        try
        {
            request = await httpContext.Request
                .ReadFromJsonAsync<SubmitPluginRequest>(cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid JSON body. Expected { pluginId: Guid, version?: string }" });
        }

        if (request is null || request.PluginId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "pluginId is required" });
        }

        // 5.5.2/5.5.3: Determine queue priority based on author karma
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        Guid? authorId = await GetAddOnAuthorIdAsync(db, request.PluginId, httpContext.RequestAborted);

        int priority = 0;
        if (authorId.HasValue)
        {
            IKarmaServicePort karmaService = httpContext.RequestServices.GetRequiredService<IKarmaServicePort>();
            KarmaSummary summary = await karmaService.GetKarmaAsync(authorId.Value, httpContext.RequestAborted);
            priority = GetPriorityFromKarma(summary.KarmaPoints);
        }

        IAnalysisQueue queue = httpContext.RequestServices.GetRequiredService<IAnalysisQueue>();
        Guid jobId = await queue.EnqueueAsync(
            request.PluginId,
            request.Version ?? "latest",
            priority,
            httpContext.RequestAborted);

        // 5.1.1: Award karma for plugin submission
        if (authorId.HasValue)
        {
            IKarmaServicePort karmaService = httpContext.RequestServices.GetRequiredService<IKarmaServicePort>();
            await karmaService.AddKarmaAsync(
                authorId.Value, 10, "plugin_submitted",
                $"Plugin {request.PluginId} submitted for analysis",
                httpContext.RequestAborted);
        }

        return Results.Json(
            new { jobId, message = "Plugin submitted for analysis" },
            statusCode: StatusCodes.Status202Accepted);
    }

    /// <summary>Determines queue priority based on karma points.</summary>
    private static int GetPriorityFromKarma(int karmaPoints) => karmaPoints switch
    {
        >= 200 => 20,  // Highest priority
        >= 100 => 10,  // Fast-track
        >= 50 => 5,    // Elevated
        _ => 0,         // Default
    };

    /// <summary>Gets the plugin author's user ID from the plugins table.</summary>
    private static async Task<Guid?> GetAddOnAuthorIdAsync(MarketplaceDbContext db, Guid pluginId, CancellationToken ct)
    {
        AddOnEntity? plugin = await db.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pluginId, ct);

        if (plugin is null)
            return null;

        // Prefer OwnerUserId (GUID FK), fall back to parsing Author (string) as GUID
        if (plugin.OwnerUserId.HasValue)
            return plugin.OwnerUserId.Value;

        if (Guid.TryParse(plugin.Author, out Guid parsed))
            return parsed;

        return null;
    }

    private static async Task<IResult> GetAnalysisHandler(Guid pluginId, HttpContext httpContext)
    {
        if (pluginId == Guid.Empty)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    { "pluginId", ["Plugin ID is required"] }
                });
        }

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var result = await db.AnalysisResults
            .Where(ar => ar.PluginId == pluginId)
            .OrderByDescending(ar => ar.AnalysisCompletedAt)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (result is null)
        {
            return Results.NotFound(new { error = "No analysis results found for this plugin" });
        }

        return Results.Ok(new
        {
            id = result.Id,
            pluginId = result.PluginId,
            pluginVersion = result.PluginVersion,
            overallScore = result.TotalScore,
            staticScores = new
            {
                eslint = result.StaticEslintScore,
                semgrep = result.StaticSemgrepScore,
                gitleaks = result.StaticGitleaksScore,
                trivy = result.StaticTrivyScore,
            },
            dynamicScore = result.DynamicBehaviorScore,
            findings = new
            {
                staticFindings = JsonSerializer.Deserialize<JsonElement>(result.StaticFindings),
                dynamicFindings = JsonSerializer.Deserialize<JsonElement>(result.DynamicFindings),
            },
            status = result.Status,
            weights = new
            {
                staticWeight = result.StaticWeight,
                dynamicWeight = result.DynamicWeight,
            },
            thresholds = new
            {
                pass = result.PassThreshold,
                fail = result.FailThreshold,
            },
            completedAt = result.AnalysisCompletedAt,
        });
    }

    private static async Task<IResult> SubmitAppealHandler(Guid pluginId, HttpContext httpContext)
    {
        if (pluginId == Guid.Empty)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { { "pluginId", ["Plugin ID is required"] } });
        }

        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        SubmitAppealBody? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<SubmitAppealBody>(cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid JSON body. Expected { reason: string, findingId?: string, evidence?: string }" });
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Reason))
        {
            return Results.BadRequest(new { error = "reason is required" });
        }

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();

        // Check existing pending appeal
        bool alreadyAppealed = await db.Appeals.AnyAsync(
            a => a.PluginId == pluginId && a.Status == "pending",
            httpContext.RequestAborted);

        if (alreadyAppealed)
        {
            return Results.Json(
                new { error = "An appeal for this plugin is already pending" },
                statusCode: StatusCodes.Status409Conflict);
        }

        // Find latest analysis result
        var analysisResult = await db.AnalysisResults
            .Where(ar => ar.PluginId == pluginId)
            .OrderByDescending(ar => ar.AnalysisCompletedAt)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        var appeal = new AppealEntity
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            AnalysisResultId = analysisResult?.Id,
            AuthorId = currentUser.UserId.Value,
            Reason = body.Reason,
            Evidence = body.Evidence,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Appeals.Add(appeal);
        await db.SaveChangesAsync(httpContext.RequestAborted);

        return Results.Json(
            new { appealId = appeal.Id, message = "Appeal submitted successfully" },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetAppealStatusHandler(Guid pluginId, HttpContext httpContext)
    {
        if (pluginId == Guid.Empty)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { { "pluginId", ["Plugin ID is required"] } });
        }

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var appeal = await db.Appeals
            .Where(a => a.PluginId == pluginId && a.Status == "pending")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(httpContext.RequestAborted);

        if (appeal is null)
        {
            return Results.NotFound(new { error = "No pending appeal found for this plugin" });
        }

        return Results.Ok(new
        {
            appealId = appeal.Id,
            status = appeal.Status,
            reason = appeal.Reason,
            evidence = appeal.Evidence,
            reviewedBy = appeal.ReviewedBy,
            reviewedAt = appeal.ReviewedAt,
            resolution = appeal.Resolution,
            createdAt = appeal.CreatedAt,
        });
    }

    private static async Task<IResult> ApprovePluginHandler(
        Guid orgId,
        Guid pluginId,
        HttpContext httpContext)
    {
        if (orgId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "orgId", ["Organization ID is required"] } });

        if (pluginId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "pluginId", ["Plugin ID is required"] } });

        string pluginVersion = "latest";
        try
        {
            ApprovePluginRequest? body = await httpContext.Request
                .ReadFromJsonAsync<ApprovePluginRequest>(cancellationToken: httpContext.RequestAborted);
            if (body?.PluginVersion is not null)
                pluginVersion = body.PluginVersion;
        }
        catch (JsonException)
        {
            // No body or invalid JSON — use default "latest"
        }

        ApproveAddOnForOrgUseCase useCase = httpContext.RequestServices.GetRequiredService<ApproveAddOnForOrgUseCase>();

        try
        {
            SafeZoneEntryDto? entry = await useCase.ExecuteAsync(orgId, pluginId, pluginVersion, httpContext.RequestAborted);
            return Results.Json(entry, statusCode: StatusCodes.Status201Created);
        }
        catch (Core.Shared.Exceptions.ProblemDetailsException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
        }
    }

    private static async Task<IResult> ListSafeZonePluginsHandler(
        Guid orgId,
        HttpContext httpContext)
    {
        if (orgId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "orgId", ["Organization ID is required"] } });

        ListSafeZoneAddOnsUseCase useCase = httpContext.RequestServices.GetRequiredService<ListSafeZoneAddOnsUseCase>();

        IReadOnlyList<SafeZonePluginDetailDto> plugins = await useCase.ExecuteAsync(orgId, httpContext.RequestAborted);

        return Results.Ok(plugins);
    }

    private static async Task<IResult> ListPendingSafeZoneAddOnsHandler(
        Guid orgId,
        HttpContext httpContext)
    {
        if (orgId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "orgId", ["Organization ID is required"] } });

        ListPendingSafeZoneAddOnsUseCase useCase = httpContext.RequestServices.GetRequiredService<ListPendingSafeZoneAddOnsUseCase>();

        try
        {
            IReadOnlyList<PendingSafeZonePluginDto> plugins = await useCase.ExecuteAsync(orgId, httpContext.RequestAborted);

            // 5.5.4: Enrich with author reputation info
            var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
            var ct = httpContext.RequestAborted;
            IKarmaServicePort karmaService = httpContext.RequestServices.GetRequiredService<IKarmaServicePort>();

            var enriched = new List<object>();
            foreach (var plugin in plugins)
            {
                // Get the plugin's author
                AddOnEntity? pluginEntity = await db.Plugins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == plugin.PluginId, ct);

                object? authorReputation = null;
                if (pluginEntity is not null)
                {
                    Guid? authorId = pluginEntity.OwnerUserId;
                    if (authorId.HasValue)
                    {
                        KarmaSummary summary = await karmaService.GetKarmaAsync(authorId.Value, ct);
                        authorReputation = new
                        {
                            authorId = authorId.Value,
                            karmaPoints = summary.KarmaPoints,
                            level = summary.Level,
                            badges = summary.Badges,
                        };
                    }
                }

                enriched.Add(new
                {
                    pluginId = plugin.PluginId,
                    name = plugin.Name,
                    slug = plugin.Slug,
                    securityScore = plugin.SecurityScore,
                    securityStatus = plugin.SecurityStatus,
                    author = authorReputation,
                });
            }

            return Results.Ok(new { items = enriched, count = enriched.Count });
        }
        catch (Core.Shared.Exceptions.ProblemDetailsException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
        }
    }

    private static async Task<IResult> ListGlobalSafeZonePluginsHandler(
        HttpContext httpContext)
    {
        ListSafeZoneAddOnsUseCase useCase = httpContext.RequestServices.GetRequiredService<ListSafeZoneAddOnsUseCase>();

        IReadOnlyList<SafeZonePluginDetailDto> plugins = await useCase.ExecuteAsync(Guid.Empty, httpContext.RequestAborted);

        return Results.Ok(plugins);
    }

    private static async Task<IResult> BlockGlobalPluginHandler(
        Guid orgId,
        Guid pluginId,
        HttpContext httpContext)
    {
        if (orgId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "orgId", ["Organization ID is required"] } });

        if (pluginId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "pluginId", ["Plugin ID is required"] } });

        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        ISafeZoneStorePort store = httpContext.RequestServices.GetRequiredService<ISafeZoneStorePort>();

        try
        {
            await store.BlockGlobalAddOnAsync(orgId, pluginId, currentUser.UserId.Value, httpContext.RequestAborted);
            return Results.Ok(new { message = "Global plugin blocked for this organization." });
        }
        catch (Core.Shared.Exceptions.ProblemDetailsException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
        }
    }

    private static async Task<IResult> UnblockGlobalPluginHandler(
        Guid orgId,
        Guid pluginId,
        HttpContext httpContext)
    {
        if (orgId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "orgId", ["Organization ID is required"] } });

        if (pluginId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "pluginId", ["Plugin ID is required"] } });

        ISafeZoneStorePort store = httpContext.RequestServices.GetRequiredService<ISafeZoneStorePort>();

        await store.UnblockGlobalAddOnAsync(orgId, pluginId, httpContext.RequestAborted);
        return Results.Ok(new { message = "Global plugin unblocked for this organization." });
    }

    private static async Task<IResult> RequestSafeZoneApprovalHandler(
        Guid orgId,
        HttpContext httpContext)
    {
        if (orgId == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]> { { "orgId", ["Organization ID is required"] } });

        RequestApprovalBody? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<RequestApprovalBody>(cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid JSON body. Expected { pluginId: Guid, pluginVersion?: string }" });
        }

        if (body is null || body.PluginId == Guid.Empty)
            return Results.BadRequest(new { error = "pluginId is required" });

        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        IMembershipStorePort membershipStore = httpContext.RequestServices.GetRequiredService<IMembershipStorePort>();
        var memberDto = await membershipStore.FindMemberAsync(orgId, currentUser.UserId.Value, httpContext.RequestAborted);
        if (memberDto is null)
            return Results.Json(new { error = "You are not a member of this organization." }, statusCode: StatusCodes.Status403Forbidden);

        ISafeZoneStorePort store = httpContext.RequestServices.GetRequiredService<ISafeZoneStorePort>();
        (bool eligible, string? reason) = await store.IsAddOnEligibleAsync(body.PluginId, httpContext.RequestAborted);
        if (!eligible)
            return Results.BadRequest(new { error = reason ?? "Plugin is not eligible for approval." });

        return Results.Ok(new
        {
            message = "Approval request recorded. An admin will review your request.",
            orgId,
            pluginId = body.PluginId,
            pluginVersion = body.PluginVersion ?? "latest",
        });
    }

    // =========================================================================
    // Phase 4 Handlers
    // =========================================================================

    /// <summary>A4: Aggregate metrics for control center dashboard.</summary>
    private static async Task<IResult> GetAdminMetricsHandler(HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        // Overview counts
        int totalAnalyzed = await db.AnalysisResults.CountAsync(ct);
        int totalPassed = await db.AnalysisResults.CountAsync(ar => ar.Status == "passed", ct);
        int totalFailed = await db.AnalysisResults.CountAsync(ar => ar.Status == "failed", ct);
        int totalInReview = await db.AnalysisResults.CountAsync(ar => ar.Status == "in_review", ct);

        // Queue status
        int queuedCount = await db.AnalysisJobs.CountAsync(j => j.Status == "queued", ct);
        int processingCount = await db.AnalysisJobs.CountAsync(j => j.Status == "processing", ct);

        // Appeals
        int pendingAppeals = await db.Appeals.CountAsync(a => a.Status == "pending", ct);

        // Average resolution time in hours for resolved appeals
        var resolvedAppeals = await db.Appeals
            .Where(a => a.Status != "pending" && a.ReviewedAt != null)
            .Select(a => new { a.CreatedAt, a.ReviewedAt })
            .ToListAsync(ct);
        double avgResolutionHours = resolvedAppeals.Any()
            ? resolvedAppeals.Average(a => (a.ReviewedAt!.Value - a.CreatedAt).TotalHours)
            : 0;

        // Recent analyses (last 24h)
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        int recentAnalyses = await db.AnalysisResults.CountAsync(ar => ar.CreatedAt >= cutoff, ct);

        // Top findings — parse static_findings JSONB and count severy type
        var allFindings = await db.AnalysisResults
            .Where(ar => ar.StaticFindings != null && ar.StaticFindings != "[]")
            .Select(ar => ar.StaticFindings)
            .ToListAsync(ct);
        var topFindings = new Dictionary<string, int>();
        foreach (var findingsJson in allFindings)
        {
            try
            {
                var findings = JsonSerializer.Deserialize<JsonElement>(findingsJson);
                if (findings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in findings.EnumerateArray())
                    {
                        string? type = f.TryGetProperty("type", out var t) ? t.GetString() : null;
                        string? message = f.TryGetProperty("message", out var m) ? m.GetString() : null;
                        string key = type ?? message ?? "unknown";
                        topFindings[key] = topFindings.GetValueOrDefault(key) + 1;
                    }
                }
            }
            catch (JsonException) { }
        }
        var topFindingsList = topFindings
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new { finding = kv.Key, count = kv.Value })
            .ToList();

        // ── 5.5.5: Reputation analytics ────────────────────────────────────
        int totalAuthors = await db.AuthorReputations.CountAsync(ct);
        double avgKarma = totalAuthors > 0
            ? await db.AuthorReputations.AverageAsync(ar => (double)ar.KarmaPoints, ct)
            : 0;

        var topAuthors = await db.AuthorReputations
            .AsNoTracking()
            .OrderByDescending(ar => ar.KarmaPoints)
            .Take(5)
            .Select(ar => new
            {
                authorId = ar.AuthorId,
                karmaPoints = ar.KarmaPoints,
                level = ar.Level,
            })
            .ToListAsync(ct);

        // Badge distribution
        int totalBadgesAwarded = await db.AuthorBadges.CountAsync(ct);
        int totalBadgeDefinitions = await db.Badges.CountAsync(ct);

        // Karma trend — events in last 30 days grouped by day
        DateTimeOffset thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var karmaTrend = await db.KarmaEvents
            .AsNoTracking()
            .Where(e => e.CreatedAt >= thirtyDaysAgo)
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new
            {
                date = g.Key,
                totalPoints = g.Sum(e => e.Points),
                eventCount = g.Count(),
            })
            .OrderBy(k => k.date)
            .ToListAsync(ct);

        return Results.Ok(new
        {
            overview = new
            {
                totalAnalyzed,
                totalPassed,
                totalFailed,
                totalInReview,
            },
            queue = new
            {
                queuedCount,
                processingCount,
            },
            appeals = new
            {
                pendingAppeals,
                avgResolutionTimeHours = Math.Round(avgResolutionHours, 1),
            },
            recentAnalyses,
            topFindings = topFindingsList,
            reputation = new
            {
                totalAuthors,
                averageKarma = Math.Round(avgKarma, 1),
                topAuthors,
                badges = new
                {
                    totalDefinitions = totalBadgeDefinitions,
                    totalAwarded = totalBadgesAwarded,
                },
                karmaTrend30Days = karmaTrend.Select(k => new
                {
                    date = k.date.ToString("yyyy-MM-dd"),
                    totalPoints = k.totalPoints,
                    eventCount = k.eventCount,
                }),
            },
        });
    }

    /// <summary>A5: List all appeals with status filter and pagination.</summary>
    private static async Task<IResult> ListAppealsHandler(HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        string? status = httpContext.Request.Query["status"];
        int page = int.TryParse(httpContext.Request.Query["page"], out var p) ? Math.Max(p, 1) : 1;
        int pageSize = 20;

        var query = db.Appeals.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        int totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                appealId = a.Id,
                pluginId = a.PluginId,
                authorId = a.AuthorId,
                reason = a.Reason,
                status = a.Status,
                reviewedBy = a.ReviewedBy,
                reviewedAt = a.ReviewedAt,
                resolution = a.Resolution,
                createdAt = a.CreatedAt,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>A5: Get appeal detail with analysis results and plugin info.</summary>
    private static async Task<IResult> GetAppealDetailHandler(Guid appealId, HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        var appeal = await db.Appeals
            .Include(a => a.AddOn)
            .Include(a => a.AnalysisResult)
            .FirstOrDefaultAsync(a => a.Id == appealId, ct);

        if (appeal is null)
            return Results.NotFound(new { error = "Appeal not found" });

        return Results.Ok(new
        {
            appealId = appeal.Id,
            pluginId = appeal.PluginId,
            pluginName = appeal.AddOn?.Name,
            analysisResultId = appeal.AnalysisResultId,
            authorId = appeal.AuthorId,
            reason = appeal.Reason,
            evidence = appeal.Evidence,
            status = appeal.Status,
            reviewedBy = appeal.ReviewedBy,
            reviewedAt = appeal.ReviewedAt,
            resolution = appeal.Resolution,
            createdAt = appeal.CreatedAt,
            analysisResult = appeal.AnalysisResult is null ? null : new
            {
                id = appeal.AnalysisResult.Id,
                totalScore = appeal.AnalysisResult.TotalScore,
                status = appeal.AnalysisResult.Status,
                staticScores = new
                {
                    eslint = appeal.AnalysisResult.StaticEslintScore,
                    semgrep = appeal.AnalysisResult.StaticSemgrepScore,
                    gitleaks = appeal.AnalysisResult.StaticGitleaksScore,
                    trivy = appeal.AnalysisResult.StaticTrivyScore,
                },
                dynamicScore = appeal.AnalysisResult.DynamicBehaviorScore,
                completedAt = appeal.AnalysisResult.AnalysisCompletedAt,
            },
        });
    }

    /// <summary>A5: Resolve an appeal (approve/reject).</summary>
    private static async Task<IResult> ResolveAppealHandler(Guid appealId, HttpContext httpContext)
    {
        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        ResolveAppealBody? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<ResolveAppealBody>(cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid JSON body. Expected { resolution: 'approved'|'rejected', notes?: string }" });
        }

        if (body is null || (body.Resolution != "approved" && body.Resolution != "rejected"))
        {
            return Results.BadRequest(new { error = "resolution must be 'approved' or 'rejected'" });
        }

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        var appeal = await db.Appeals
            .Include(a => a.AnalysisResult)
            .FirstOrDefaultAsync(a => a.Id == appealId, ct);

        if (appeal is null)
            return Results.NotFound(new { error = "Appeal not found" });

        if (appeal.Status != "pending")
            return Results.BadRequest(new { error = "Appeal is already resolved" });

        appeal.Status = body.Resolution == "approved" ? "approved" : "rejected";
        appeal.ReviewedBy = currentUser.UserId.Value;
        appeal.ReviewedAt = DateTimeOffset.UtcNow;
        appeal.Resolution = body.Notes;

        // If approved, update plugin security status
        if (body.Resolution == "approved" && appeal.AnalysisResult is not null)
        {
            appeal.AnalysisResult.Status = "passed";

            var plugin = await db.Plugins.FirstOrDefaultAsync(p => p.Id == appeal.PluginId, ct);
            if (plugin is not null)
            {
                plugin.SecurityStatus = "passed";
                plugin.SecurityScore = appeal.AnalysisResult.TotalScore;
            }
        }

        await db.SaveChangesAsync(ct);

        // 5.1.3: Award karma for appeal resolution
        IKarmaServicePort karmaService = httpContext.RequestServices.GetRequiredService<IKarmaServicePort>();
        if (body.Resolution == "approved")
        {
            await karmaService.AddKarmaAsync(
                appeal.AuthorId, 30, "appeal_won",
                $"Appeal for plugin {appeal.PluginId} was approved",
                ct);
        }
        else
        {
            await karmaService.AddKarmaAsync(
                appeal.AuthorId, -10, "appeal_lost",
                $"Appeal for plugin {appeal.PluginId} was rejected",
                ct);
        }

        return Results.Ok(new { message = "Appeal resolved", status = appeal.Status });
    }

    /// <summary>A6: Get current analysis config.</summary>
    private static async Task<IResult> GetAnalysisConfigHandler(HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        var config = await db.AnalysisConfig.FirstOrDefaultAsync(ct);
        if (config is null)
        {
            // Return defaults
            config = new AnalysisConfigEntity();
        }

        return Results.Ok(new
        {
            staticWeight = config.StaticWeight,
            dynamicWeight = config.DynamicWeight,
            passThreshold = config.PassThreshold,
            failThreshold = config.FailThreshold,
            maxWorkers = config.MaxWorkers,
            retryLimit = config.RetryLimit,
            analysisTimeoutSeconds = config.AnalysisTimeoutSeconds,
            updatedAt = config.UpdatedAt,
            updatedBy = config.UpdatedBy,
        });
    }

    /// <summary>A6: Update analysis config.</summary>
    private static async Task<IResult> UpdateAnalysisConfigHandler(HttpContext httpContext)
    {
        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        UpdateAnalysisConfigBody? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<UpdateAnalysisConfigBody>(cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid JSON body" });
        }

        if (body is null)
            return Results.BadRequest(new { error = "Request body is required" });

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        var config = await db.AnalysisConfig.FirstOrDefaultAsync(ct);
        if (config is null)
        {
            config = new AnalysisConfigEntity { Id = 1 };
            db.AnalysisConfig.Add(config);
        }

        // Snapshot previous config for audit log
        var previousConfig = new
        {
            config.StaticWeight,
            config.DynamicWeight,
            config.PassThreshold,
            config.FailThreshold,
            config.MaxWorkers,
            config.RetryLimit,
            config.AnalysisTimeoutSeconds,
        };

        if (body.StaticWeight.HasValue) config.StaticWeight = body.StaticWeight.Value;
        if (body.DynamicWeight.HasValue) config.DynamicWeight = body.DynamicWeight.Value;
        if (body.PassThreshold.HasValue) config.PassThreshold = body.PassThreshold.Value;
        if (body.FailThreshold.HasValue) config.FailThreshold = body.FailThreshold.Value;
        if (body.MaxWorkers.HasValue) config.MaxWorkers = body.MaxWorkers.Value;
        if (body.RetryLimit.HasValue) config.RetryLimit = body.RetryLimit.Value;
        if (body.AnalysisTimeoutSeconds.HasValue) config.AnalysisTimeoutSeconds = body.AnalysisTimeoutSeconds.Value;

        // Validate
        if (config.StaticWeight < 0 || config.DynamicWeight < 0 ||
            Math.Abs(config.StaticWeight + config.DynamicWeight - 1.0m) > 0.01m)
        {
            return Results.BadRequest(new { error = "Static and dynamic weights must sum to 1.0" });
        }
        if (config.PassThreshold <= config.FailThreshold)
        {
            return Results.BadRequest(new { error = "passThreshold must be greater than failThreshold" });
        }
        if (config.MaxWorkers < 1) return Results.BadRequest(new { error = "maxWorkers must be at least 1" });
        if (config.RetryLimit < 0) return Results.BadRequest(new { error = "retryLimit must be non-negative" });

        config.UpdatedAt = DateTimeOffset.UtcNow;
        config.UpdatedBy = currentUser.UserId.Value;

        // Record change in audit log
        var logEntry = new ConfigChangeLogEntity
        {
            Id = Guid.NewGuid(),
            ChangedBy = currentUser.UserId.Value,
            PreviousConfig = JsonSerializer.Serialize(previousConfig),
            NewConfig = JsonSerializer.Serialize(new
            {
                config.StaticWeight,
                config.DynamicWeight,
                config.PassThreshold,
                config.FailThreshold,
                config.MaxWorkers,
                config.RetryLimit,
                config.AnalysisTimeoutSeconds,
            }),
            ChangeDescription = "Analysis configuration updated",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.ConfigChangeLogs.Add(logEntry);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Analysis config updated" });
    }

    /// <summary>A6: Get config change history.</summary>
    private static async Task<IResult> GetConfigHistoryHandler(HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        int page = int.TryParse(httpContext.Request.Query["page"], out var p) ? Math.Max(p, 1) : 1;
        int pageSize = 20;

        var query = db.ConfigChangeLogs.OrderByDescending(c => c.CreatedAt);
        int totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                id = c.Id,
                changedBy = c.ChangedBy,
                previousConfig = c.PreviousConfig,
                newConfig = c.NewConfig,
                changeDescription = c.ChangeDescription,
                createdAt = c.CreatedAt,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>A7: Get unified audit logs.</summary>
    private static async Task<IResult> GetAuditLogsHandler(HttpContext httpContext)
    {
        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        DateTimeOffset? from = httpContext.Request.Query.TryGetValue("from", out var f) && DateTimeOffset.TryParse(f, out var fromVal) ? fromVal : null;
        DateTimeOffset? to = httpContext.Request.Query.TryGetValue("to", out var t) && DateTimeOffset.TryParse(t, out var toVal) ? toVal : null;
        string? type = httpContext.Request.Query["type"];
        int page = int.TryParse(httpContext.Request.Query["page"], out var p) ? Math.Max(p, 1) : 1;
        int pageSize = 20;

        var events = new List<object>();

        // Karma events
        if (string.IsNullOrWhiteSpace(type) || type == "karma")
        {
            var karmaQuery = db.KarmaEvents.AsQueryable();
            if (from.HasValue) karmaQuery = karmaQuery.Where(e => e.CreatedAt >= from.Value);
            if (to.HasValue) karmaQuery = karmaQuery.Where(e => e.CreatedAt <= to.Value);

            var karmaEvents = await karmaQuery
                .OrderByDescending(e => e.CreatedAt)
                .Take(pageSize)
                .Select(e => new
                {
                    timestamp = e.CreatedAt,
                    eventType = "karma",
                    description = e.Description ?? e.EventType,
                    actorId = (Guid?)e.AuthorId,
                    details = new { e.EventType, e.Points },
                })
                .ToListAsync(ct);
            events.AddRange(karmaEvents);
        }

        // Analysis results events
        if (string.IsNullOrWhiteSpace(type) || type == "analysis")
        {
            var analysisQuery = db.AnalysisResults.AsQueryable();
            if (from.HasValue) analysisQuery = analysisQuery.Where(e => e.CreatedAt >= from.Value);
            if (to.HasValue) analysisQuery = analysisQuery.Where(e => e.CreatedAt <= to.Value);

            var analysisEvents = await analysisQuery
                .OrderByDescending(e => e.CreatedAt)
                .Take(pageSize)
                .Select(e => new
                {
                    timestamp = e.CreatedAt,
                    eventType = "analysis",
                    description = "Analysis completed for plugin",
                    actorId = (Guid?)null,
                    details = new { e.PluginId, e.Status, e.TotalScore },
                })
                .ToListAsync(ct);
            events.AddRange(analysisEvents);
        }

        // Appeal events
        if (string.IsNullOrWhiteSpace(type) || type == "appeal")
        {
            var appealQuery = db.Appeals.AsQueryable();
            if (from.HasValue) appealQuery = appealQuery.Where(e => e.CreatedAt >= from.Value);
            if (to.HasValue) appealQuery = appealQuery.Where(e => e.CreatedAt <= to.Value);

            var appealEvents = await appealQuery
                .OrderByDescending(e => e.CreatedAt)
                .Take(pageSize)
                .Select(e => new
                {
                    timestamp = e.CreatedAt,
                    eventType = "appeal",
                    description = e.Status == "pending" ? "Appeal submitted" : $"Appeal {e.Status}",
                    actorId = e.ReviewedBy,
                    details = new { e.Id, e.PluginId, e.Status, e.Reason },
                })
                .ToListAsync(ct);
            events.AddRange(appealEvents);
        }

        // Safe zone approval events
        if (string.IsNullOrWhiteSpace(type) || type == "approval")
        {
            var szQuery = db.SafeZonePlugins.AsQueryable();
            if (from.HasValue) szQuery = szQuery.Where(e => e.ApprovedAt >= from.Value);
            if (to.HasValue) szQuery = szQuery.Where(e => e.ApprovedAt <= to.Value);

            var szEvents = await szQuery
                .OrderByDescending(e => e.ApprovedAt)
                .Take(pageSize)
                .Select(e => new
                {
                    timestamp = e.ApprovedAt,
                    eventType = "approval",
                    description = "Plugin approved for safe zone",
                    actorId = (Guid?)e.ApprovedBy,
                    details = new { e.PluginId, e.OrgId, e.PluginVersion },
                })
                .ToListAsync(ct);
            events.AddRange(szEvents);
        }

        // Sort unified events by timestamp desc
        var sorted = events
            .OrderByDescending(e => ((dynamic)e).timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Results.Ok(new { items = sorted, totalCount = events.Count, page, pageSize });
    }

    /// <summary>A8: Get current user's notifications.</summary>
    private static async Task<IResult> GetNotificationsHandler(HttpContext httpContext)
    {
        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        bool unreadOnly = httpContext.Request.Query.TryGetValue("unreadOnly", out var uo) && bool.TryParse(uo, out var ur) && ur;
        int page = int.TryParse(httpContext.Request.Query["page"], out var p) ? Math.Max(p, 1) : 1;
        int pageSize = 20;

        var query = db.Notifications.Where(n => n.UserId == currentUser.UserId.Value);
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        int totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                id = n.Id,
                type = n.Type,
                title = n.Title,
                message = n.Message,
                isRead = n.IsRead,
                createdAt = n.CreatedAt,
            })
            .ToListAsync(ct);

        return Results.Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>A8: Mark notification as read.</summary>
    private static async Task<IResult> MarkNotificationReadHandler(Guid notificationId, HttpContext httpContext)
    {
        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == currentUser.UserId.Value, ct);

        if (notification is null)
            return Results.NotFound(new { error = "Notification not found" });

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Notification marked as read" });
    }

    /// <summary>A8: Mark all notifications as read.</summary>
    private static async Task<IResult> MarkAllNotificationsReadHandler(HttpContext httpContext)
    {
        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        await db.Notifications
            .Where(n => n.UserId == currentUser.UserId.Value && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);

        return Results.Ok(new { message = "All notifications marked as read" });
    }

    /// <summary>A8: Update notification preferences.</summary>
    private static async Task<IResult> UpdateNotificationPreferencesHandler(HttpContext httpContext)
    {
        ICurrentUser currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Results.Json(new { error = "Authentication required." }, statusCode: StatusCodes.Status401Unauthorized);

        UpdateNotificationPreferencesBody? body;
        try
        {
            body = await httpContext.Request
                .ReadFromJsonAsync<UpdateNotificationPreferencesBody>(cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Invalid JSON body" });
        }

        if (body is null)
            return Results.BadRequest(new { error = "Request body is required" });

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        var prefs = await db.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId.Value, ct);

        if (prefs is null)
        {
            prefs = new UserNotificationPreferencesEntity
            {
                UserId = currentUser.UserId.Value,
                EmailAlerts = body.EmailAlerts ?? true,
                InAppAlerts = body.InAppAlerts ?? true,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.UserNotificationPreferences.Add(prefs);
        }
        else
        {
            if (body.EmailAlerts.HasValue) prefs.EmailAlerts = body.EmailAlerts.Value;
            if (body.InAppAlerts.HasValue) prefs.InAppAlerts = body.InAppAlerts.Value;
            prefs.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Notification preferences updated" });
    }

    private static async Task<IResult> GetReputationLeaderboardHandler(HttpContext httpContext)
    {
        string period = httpContext.Request.Query["period"].FirstOrDefault() ?? "all_time";
        string? orgIdStr = httpContext.Request.Query["orgId"].FirstOrDefault();
        Guid? orgId = Guid.TryParse(orgIdStr, out var parsed) ? parsed : null;

        ILeaderboardPort leaderboard = httpContext.RequestServices.GetRequiredService<ILeaderboardPort>();
        IReadOnlyList<LeaderboardEntryDto> entries = await leaderboard.GetLeaderboardAsync(
            period, orgId, limit: 20, ct: httpContext.RequestAborted);

        return Results.Ok(new
        {
            period,
            orgId,
            entries = entries.Select(e => new
            {
                rank = e.Rank,
                authorId = e.AuthorId,
                karmaPoints = e.KarmaPoints,
                level = e.Level,
                badgeCount = e.BadgeCount,
            }),
        });
    }

    private static async Task<IResult> GetAuthorReputationHandler(Guid authorId, HttpContext httpContext)
    {
        if (authorId == Guid.Empty)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    { "authorId", ["Author ID is required"] }
                });
        }

        var db = httpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
        var ct = httpContext.RequestAborted;

        IKarmaServicePort karmaService = httpContext.RequestServices.GetRequiredService<IKarmaServicePort>();
        KarmaSummary summary = await karmaService.GetKarmaAsync(authorId, ct);

        IBadgeServicePort badgeService = httpContext.RequestServices.GetRequiredService<IBadgeServicePort>();
        IReadOnlyList<AuthorBadgeDto> badges = await badgeService.GetAuthorBadgesAsync(authorId, ct);

        // Aggregate stats from plugins + analysis + appeals
        var pluginIds = await db.Plugins
            .AsNoTracking()
            .Where(p => p.OwnerUserId == authorId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        int pluginsSubmitted = pluginIds.Count;
        int pluginsPassed = await db.AnalysisResults
            .CountAsync(ar => pluginIds.Contains(ar.PluginId) && ar.Status == "passed", ct);
        int pluginsFailed = await db.AnalysisResults
            .CountAsync(ar => pluginIds.Contains(ar.PluginId) && ar.Status == "failed", ct);
        int appealsWon = await db.Appeals
            .CountAsync(a => a.AuthorId == authorId && a.Status == "approved", ct);
        int appealsLost = await db.Appeals
            .CountAsync(a => a.AuthorId == authorId && a.Status == "rejected", ct);

        DateTimeOffset? memberSince = await db.AnalysisResults
            .Where(ar => pluginIds.Contains(ar.PluginId))
            .OrderBy(ar => ar.CreatedAt)
            .Select(ar => (DateTimeOffset?)ar.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new
        {
            authorId,
            karmaPoints = summary.KarmaPoints,
            level = summary.Level,
            badges = badges.Select(b => new
            {
                badgeId = b.BadgeId,
                name = b.Name,
                slug = b.Slug,
                description = b.Description,
                iconUrl = b.IconUrl,
                awardedAt = b.AwardedAt,
            }),
            stats = new
            {
                pluginsSubmitted,
                pluginsPassed,
                pluginsFailed,
                appealsWon,
                appealsLost,
                memberSince,
            },
        });
    }

    private static async Task<IResult> GetBadgesHandler(HttpContext httpContext)
    {
        IBadgeServicePort badgeService = httpContext.RequestServices.GetRequiredService<IBadgeServicePort>();
        IReadOnlyList<BadgeDefinitionDto> badges = await badgeService.GetAllBadgesAsync(httpContext.RequestAborted);

        return Results.Ok(new
        {
            badges = badges.Select(b => new
            {
                id = b.Id,
                name = b.Name,
                slug = b.Slug,
                description = b.Description,
                iconUrl = b.IconUrl,
                requirements = b.Requirements,
                tier = b.Tier,
            }),
        });
    }
}
