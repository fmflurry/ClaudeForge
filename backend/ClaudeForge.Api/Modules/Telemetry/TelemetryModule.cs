using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.Telemetry.Ports;
using ClaudeForge.Application.Modules.Telemetry.UseCases;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.RateLimiting;

namespace ClaudeForge.Api.Modules.Telemetry;

/// <summary>
/// Feature module for Telemetry endpoints:
///   POST /api/v1/telemetry/events            → 202 Accepted (fire-and-forget)
///   GET  /api/v1/plugins/{pluginId}/telemetry/summary → 200 TelemetrySummaryDto
///
/// Raw events are never exposed via API. Only aggregate data is returned.
/// </summary>
public sealed class TelemetryModule : IModule
{
    private const string TelemetryRateLimitPolicy = "telemetry-ingest-limit";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // In-memory cache for summary results (5-minute window)
        services.AddMemoryCache();

        // Store adapter
        services.AddScoped<ITelemetryStorePort>(sp =>
            new TelemetryStoreAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        // Use cases
        services.AddScoped<IngestTelemetryEventUseCase>();
        services.AddScoped<GetTelemetrySummaryUseCase>();

        // Retention background job (gated by Features:TelemetryRetention flag).
        // The config value is the retention window in days (int); any positive value enables the job.
        services.AddSingleton<TelemetryRetentionJob>(sp =>
        {
            IConfiguration config = sp.GetRequiredService<IConfiguration>();
            int retentionDays = config.GetValue<int>("Features:TelemetryRetention");
            bool enabled = retentionDays > 0;

            return new TelemetryRetentionJob(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<TelemetryRetentionJob>>(),
                enabled);
        });
        services.AddHostedService(sp => sp.GetRequiredService<TelemetryRetentionJob>());

        // Per-IP rate limiting for telemetry POST
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(TelemetryRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/telemetry/events", PostTelemetryEventHandler)
            .WithName("PostTelemetryEvent")
            .WithTags("Telemetry")
            .RequireRateLimiting(TelemetryRateLimitPolicy);

        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}/telemetry/summary", GetTelemetrySummaryHandler)
            .WithName("GetTelemetrySummary")
            .WithTags("Telemetry");

        return endpoints;
    }

    // =========================================================================
    // Handlers
    // =========================================================================

    private static async Task<IResult> PostTelemetryEventHandler(
        [FromBody] IngestTelemetryRequest request,
        [FromServices] IngestTelemetryEventUseCase useCase,
        CancellationToken ct)
    {
        IngestTelemetryCommand cmd = new()
        {
            EventType = request.EventType ?? string.Empty,
            PluginId = request.PluginId,
            Version = request.Version,
            AnonClientId = request.AnonClientId ?? string.Empty,
            ClientOs = request.ClientOs,
            ClientArch = request.ClientArch,
        };

        // Use-case validates and throws ProblemDetailsException on invalid input.
        // GlobalExceptionHandler maps ProblemDetailsException → 400.
        await useCase.ExecuteAsync(cmd, ct);

        return Results.Accepted();
    }

    private static async Task<IResult> GetTelemetrySummaryHandler(
        Guid pluginId,
        [FromServices] GetTelemetrySummaryUseCase useCase,
        CancellationToken ct)
    {
        TelemetrySummaryDto summary = await useCase.ExecuteAsync(pluginId, ct);
        return Results.Ok(summary);
    }
}

/// <summary>
/// JSON request body for POST /api/v1/telemetry/events.
/// All fields are nullable to allow graceful 400 validation via the use-case.
/// </summary>
internal sealed record IngestTelemetryRequest
{
    public string? EventType { get; init; }
    public Guid PluginId { get; init; }
    public string? Version { get; init; }
    public string? AnonClientId { get; init; }
    public string? ClientOs { get; init; }
    public string? ClientArch { get; init; }
}
