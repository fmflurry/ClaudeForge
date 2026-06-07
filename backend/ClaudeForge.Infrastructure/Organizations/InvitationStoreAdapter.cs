using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Organizations;

/// <summary>
/// EF Core adapter for <see cref="IInvitationStorePort"/>.
/// </summary>
public sealed class InvitationStoreAdapter : IInvitationStorePort
{
    private readonly MarketplaceDbContext _ctx;

    public InvitationStoreAdapter(MarketplaceDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<InvitationDto> CreateAsync(
        CreateInvitationRecord record,
        CancellationToken ct = default)
    {
        OrganizationInvitationEntity entity = new()
        {
            Id = record.Id,
            OrgId = record.OrgId,
            EmailNormalized = record.EmailNormalized,
            InvitedBy = record.InvitedBy,
            Role = record.Role.Value,
            Status = "pending",
            Token = record.Token,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = record.ExpiresAt,
            AcceptedAt = null,
            RevokedAt = null,
        };

        _ctx.OrganizationInvitations.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<InvitationDto?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        OrganizationInvitationEntity? entity = await _ctx.OrganizationInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<InvitationDto?> FindPendingByOrgAndEmailAsync(
        Guid orgId,
        string emailNormalized,
        CancellationToken ct = default)
    {
        OrganizationInvitationEntity? entity = await _ctx.OrganizationInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.OrgId == orgId
                    && i.EmailNormalized == emailNormalized
                    && i.Status == "pending",
                ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task UpdateStatusAsync(
        Guid id,
        string newStatus,
        DateTimeOffset? acceptedAt,
        DateTimeOffset? revokedAt,
        CancellationToken ct = default)
    {
        OrganizationInvitationEntity? entity = await _ctx.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (entity is not null)
        {
            entity.Status = newStatus;
            entity.AcceptedAt = acceptedAt;
            entity.RevokedAt = revokedAt;
            await _ctx.SaveChangesAsync(ct);
        }
    }

    private static InvitationDto ToDto(OrganizationInvitationEntity entity) => new(
        Id: entity.Id,
        OrgId: entity.OrgId,
        EmailNormalized: entity.EmailNormalized,
        InvitedBy: entity.InvitedBy,
        Role: OrgRole.Parse(entity.Role),
        Status: entity.Status,
        Token: entity.Token,
        CreatedAt: entity.CreatedAt,
        ExpiresAt: entity.ExpiresAt,
        AcceptedAt: entity.AcceptedAt,
        RevokedAt: entity.RevokedAt);
}
