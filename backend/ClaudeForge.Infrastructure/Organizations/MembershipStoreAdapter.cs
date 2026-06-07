using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Organizations;

/// <summary>
/// EF Core adapter for <see cref="IMembershipStorePort"/>.
/// Joins the Users table to return email and display name.
/// </summary>
public sealed class MembershipStoreAdapter : IMembershipStorePort
{
    private readonly MarketplaceDbContext _ctx;

    public MembershipStoreAdapter(MarketplaceDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task AddMemberAsync(
        Guid orgId,
        Guid userId,
        OrgRole role,
        CancellationToken ct = default)
    {
        OrganizationMemberEntity entity = new()
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            Role = role.Value,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _ctx.OrganizationMembers.Add(entity);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(
        Guid orgId,
        Guid userId,
        CancellationToken ct = default)
    {
        OrganizationMemberEntity? entity = await _ctx.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);

        if (entity is not null)
        {
            _ctx.OrganizationMembers.Remove(entity);
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public async Task UpdateMemberRoleAsync(
        Guid orgId,
        Guid userId,
        OrgRole newRole,
        CancellationToken ct = default)
    {
        OrganizationMemberEntity? entity = await _ctx.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.UserId == userId, ct);

        if (entity is not null)
        {
            entity.Role = newRole.Value;
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public async Task<int> CountOwnersAsync(Guid orgId, CancellationToken ct = default)
    {
        return await _ctx.OrganizationMembers
            .CountAsync(m => m.OrgId == orgId && m.Role == "owner", ct);
    }

    public async Task<MemberDto?> FindMemberAsync(
        Guid orgId,
        Guid userId,
        CancellationToken ct = default)
    {
        MemberDto? result = await _ctx.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.OrgId == orgId && m.UserId == userId)
            .Join(
                _ctx.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new MemberDto(
                    UserId: m.UserId,
                    Email: u.Email,
                    DisplayName: u.DisplayName,
                    Role: OrgRole.Parse(m.Role),
                    JoinedAt: m.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return result;
    }

    public async Task<MemberDto?> FindMemberByEmailAsync(
        Guid orgId,
        string emailNormalized,
        CancellationToken ct = default)
    {
        MemberDto? result = await _ctx.OrganizationMembers
            .AsNoTracking()
            .Join(
                _ctx.Users.AsNoTracking().Where(u => u.EmailNormalized == emailNormalized),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { Member = m, User = u })
            .Where(x => x.Member.OrgId == orgId)
            .Select(x => new MemberDto(
                UserId: x.Member.UserId,
                Email: x.User.Email,
                DisplayName: x.User.DisplayName,
                Role: OrgRole.Parse(x.Member.Role),
                JoinedAt: x.Member.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return result;
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(
        Guid orgId,
        CancellationToken ct = default)
    {
        List<MemberDto> results = await _ctx.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.OrgId == orgId)
            .Join(
                _ctx.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new MemberDto(
                    UserId: m.UserId,
                    Email: u.Email,
                    DisplayName: u.DisplayName,
                    Role: OrgRole.Parse(m.Role),
                    JoinedAt: m.CreatedAt))
            .ToListAsync(ct);

        return results;
    }

    public async Task<IReadOnlyList<OrgSummaryDto>> ListOrgsForUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        List<OrgSummaryDto> results = await _ctx.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(
                _ctx.Organizations.AsNoTracking(),
                m => m.OrgId,
                o => o.Id,
                (m, o) => new OrgSummaryDto(
                    Id: o.Id,
                    Name: o.Name,
                    Slug: o.Slug,
                    UserRole: OrgRole.Parse(m.Role)))
            .ToListAsync(ct);

        return results;
    }
}
