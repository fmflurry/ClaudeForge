using ClaudeForge.Api.Module;
using ClaudeForge.Application.Modules.PluginDistribution.Ports;
using ClaudeForge.Application.Modules.PluginDistribution.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.PluginDistribution;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ClaudeForge.Api.Modules.PluginDistribution;

/// <summary>
/// Feature module for the Plugin Distribution endpoint.
///   GET /api/v1/plugins/{pluginId:Guid}/download?version=
/// </summary>
public sealed class PluginDistributionModule : IModule
{
    private const string DownloadRateLimitPolicy = "plugin-download-limit";

    public IServiceCollection RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        // Repository adapter
        services.AddScoped<IPluginDistributionRepositoryPort>(sp =>
            new PluginDistributionRepositoryAdapter(sp.GetRequiredService<MarketplaceDbContext>()));

        // Access policy (singleton — pure logic, no I/O)
        if (!services.Any(d => d.ServiceType == typeof(IPluginAccessPolicy)))
        {
            services.AddSingleton<IPluginAccessPolicy, PluginAccessPolicy>();
        }

        // IOrgMembershipQueryPort (requires IMemoryCache)
        if (!services.Any(d => d.ServiceType == typeof(IOrgMembershipQueryPort)))
        {
            services.AddMemoryCache();
            services.AddScoped<IOrgMembershipQueryPort>(sp =>
                new OrgMembershipQueryAdapter(
                    sp.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<MarketplaceDbContext>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
        }

        // Use case
        services.AddScoped<DownloadPluginUseCase>();

        // Per-IP fixed-window rate limiting for the download endpoint
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(DownloadRateLimitPolicy, httpContext =>
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
        endpoints.MapGet("/api/v1/plugins/{pluginId:guid}/download", DownloadPluginHandler)
            .WithName("DownloadPlugin")
            .WithTags("PluginDistribution")
            .RequireRateLimiting(DownloadRateLimitPolicy);

        return endpoints;
    }

    // =========================================================================
    // Handlers
    // =========================================================================

    private static async Task<IResult> DownloadPluginHandler(
        Guid pluginId,
        [FromQuery] string? version,
        [FromServices] DownloadPluginUseCase useCase,
        HttpContext httpContext)
    {
        DownloadResult result = await useCase.ExecuteAsync(pluginId, version, httpContext.RequestAborted);

        // Set response headers before streaming
        httpContext.Response.Headers.ETag = $"\"{result.Sha256}\"";
        httpContext.Response.ContentLength = result.SizeBytes;

        return Results.File(
            fileStream: result.Stream,
            contentType: result.ContentType,
            fileDownloadName: result.FileName);
    }
}
