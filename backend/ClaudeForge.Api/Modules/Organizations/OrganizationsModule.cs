using System.Threading.RateLimiting;
using ClaudeForge.Api.Infrastructure.Context;
using ClaudeForge.Api.Infrastructure.Serialization;
using ClaudeForge.Api.Module;
using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Authorization;
using ClaudeForge.Infrastructure.Organizations;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Api.Modules.Organizations;

/// <summary>
/// Feature module for the Organizations domain.
/// Registers all ports, adapters, use-cases, and HTTP endpoints under /api/v1/orgs.
/// </summary>
public sealed class OrganizationsModule : IModule
{
    private const string InviteRateLimitPolicy = "auth-invite-limit";

    public IServiceCollection RegisterModule(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register OrgRole JSON converter so value objects serialize as plain strings
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new OrgRoleJsonConverter());
        });

        // IHttpContextAccessor is needed by HttpContextCurrentUser
        services.AddHttpContextAccessor();

        // ICurrentUser → HttpContextCurrentUser (Scoped — per request)
        // Only register if not already registered (integration tests replace this with their stub)
        if (!services.Any(d => d.ServiceType == typeof(ICurrentUser)))
        {
            services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        }

        // Ports → Adapters (Scoped — share the per-request DbContext)
        services.AddScoped<IOrganizationStorePort, OrganizationStoreAdapter>();
        services.AddScoped<IMembershipStorePort, MembershipStoreAdapter>();
        services.AddScoped<IInvitationStorePort, InvitationStoreAdapter>();
        services.AddScoped<IOrgAuditLogPort, OrgAuditLogAdapter>();
        services.AddScoped<IInvitationEmailPort>(sp =>
            new SmtpInvitationEmailAdapter(sp.GetRequiredService<IConfiguration>()));

        // Use-cases (Scoped)
        services.AddScoped<CreateOrganizationUseCase>();
        services.AddScoped<ListUserOrganizationsUseCase>();
        services.AddScoped<ListOrgMembersUseCase>();
        services.AddScoped<RemoveMemberUseCase>();
        services.AddScoped<ChangeMemberRoleUseCase>();
        services.AddScoped<IssueInvitationUseCase>();
        services.AddScoped<AcceptInvitationUseCase>();
        services.AddScoped<RevokeInvitationUseCase>();

        // IOrgMembershipQueryPort is registered by Program or a shared module.
        // Register with in-memory cache support if not already registered.
        if (!services.Any(d => d.ServiceType == typeof(IOrgMembershipQueryPort)))
        {
            services.AddMemoryCache();
            services.AddScoped<IOrgMembershipQueryPort>(sp =>
                new OrgMembershipQueryAdapter(
                    sp.GetRequiredService<IDbContextFactory<MarketplaceDbContext>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));
        }

        // ── Group 8 — Per-IP rate limiting for invitation endpoint ───────────────
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(InviteRateLimitPolicy, httpContext =>
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
        RouteGroupBuilder orgs = endpoints.MapGroup("/api/v1/orgs").WithTags("Organizations");

        // POST /api/v1/orgs — Create organization
        orgs.MapPost("/", async (
            CreateOrganizationRequest req,
            CreateOrganizationUseCase useCase,
            CancellationToken ct) =>
        {
            CreateOrganizationCommand command = new(req.Name, req.Slug);
            OrganizationDto result = await useCase.ExecuteAsync(command, ct);
            return Results.Created($"/api/v1/orgs/{result.Id}", result);
        });

        // GET /api/v1/orgs — List user organizations
        orgs.MapGet("/", async (
            ListUserOrganizationsUseCase useCase,
            CancellationToken ct) =>
        {
            IReadOnlyList<OrgSummaryDto> result = await useCase.ExecuteAsync(ct);
            return Results.Ok(new { data = result });
        });

        // GET /api/v1/orgs/{orgId}/members — List org members
        orgs.MapGet("/{orgId:guid}/members", async (
            Guid orgId,
            ListOrgMembersUseCase useCase,
            CancellationToken ct) =>
        {
            IReadOnlyList<MemberDto> result = await useCase.ExecuteAsync(orgId, ct);
            return Results.Ok(new { data = result });
        });

        // POST /api/v1/orgs/{orgId}/invitations — Issue invitation
        orgs.MapPost("/{orgId:guid}/invitations", async (
            Guid orgId,
            IssueInvitationRequest req,
            IssueInvitationUseCase useCase,
            CancellationToken ct) =>
        {
            OrgRole role = OrgRole.Parse(req.Role ?? "member");
            InvitationDto result = await useCase.ExecuteAsync(orgId, req.Email, role, ct);
            return Results.Created($"/api/v1/orgs/{orgId}/invitations/{result.Id}", result);
        })
        .RequireRateLimiting(InviteRateLimitPolicy);

        // POST /api/v1/orgs/{orgId}/invitations/{id}/accept — Accept invitation
        orgs.MapPost("/{orgId:guid}/invitations/{id:guid}/accept", async (
            Guid orgId,
            Guid id,
            AcceptInvitationUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(orgId, id, ct);
            return Results.Ok();
        });

        // POST /api/v1/orgs/{orgId}/invitations/{id}/revoke — Revoke invitation
        orgs.MapPost("/{orgId:guid}/invitations/{id:guid}/revoke", async (
            Guid orgId,
            Guid id,
            RevokeInvitationUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(orgId, id, ct);
            return Results.Ok();
        });

        // DELETE /api/v1/orgs/{orgId}/members/{userId} — Remove member
        orgs.MapDelete("/{orgId:guid}/members/{userId:guid}", async (
            Guid orgId,
            Guid userId,
            RemoveMemberUseCase useCase,
            CancellationToken ct) =>
        {
            await useCase.ExecuteAsync(orgId, userId, ct);
            return Results.NoContent();
        });

        // PATCH /api/v1/orgs/{orgId}/members/{userId} — Change member role
        orgs.MapPatch("/{orgId:guid}/members/{userId:guid}", async (
            Guid orgId,
            Guid userId,
            ChangeMemberRoleRequest req,
            ChangeMemberRoleUseCase useCase,
            CancellationToken ct) =>
        {
            OrgRole newRole = OrgRole.Parse(req.Role);
            await useCase.ExecuteAsync(orgId, userId, newRole, ct);
            return Results.Ok();
        });

        return endpoints;
    }

    // ─── Request DTOs ─────────────────────────────────────────────────────────

    private sealed record CreateOrganizationRequest(string Name, string? Slug);
    private sealed record IssueInvitationRequest(string Email, string? Role);
    private sealed record ChangeMemberRoleRequest(string Role);
}
