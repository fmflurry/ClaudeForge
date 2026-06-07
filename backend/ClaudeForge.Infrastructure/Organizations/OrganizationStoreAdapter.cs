using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Modules.Organizations.UseCases;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Organizations;

/// <summary>
/// EF Core adapter for <see cref="IOrganizationStorePort"/>.
/// Backed by <see cref="MarketplaceDbContext"/>.
/// </summary>
public sealed class OrganizationStoreAdapter : IOrganizationStorePort
{
    private readonly MarketplaceDbContext _ctx;

    public OrganizationStoreAdapter(MarketplaceDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<OrganizationDto?> FindByNameNormalizedAsync(
        string nameNormalized,
        CancellationToken ct = default)
    {
        OrganizationEntity? entity = await _ctx.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.NameNormalized == nameNormalized, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<OrganizationDto> CreateAsync(
        CreateOrganizationRecord record,
        CancellationToken ct = default)
    {
        OrganizationEntity entity = new()
        {
            Id = record.Id,
            Name = record.Name,
            NameNormalized = record.NameNormalized,
            Slug = record.Slug,
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAt,
        };

        _ctx.Organizations.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    private static OrganizationDto ToDto(OrganizationEntity entity) => new(
        Id: entity.Id,
        Name: entity.Name,
        NameNormalized: entity.NameNormalized,
        Slug: entity.Slug,
        CreatedBy: entity.CreatedBy,
        CreatedAt: entity.CreatedAt);
}
