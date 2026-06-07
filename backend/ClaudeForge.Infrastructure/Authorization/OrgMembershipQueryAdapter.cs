using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ClaudeForge.Infrastructure.Authorization;

/// <summary>
/// EF Core + in-memory-cache adapter for <see cref="IOrgMembershipQueryPort"/>.
/// Membership reads are cached per user for <see cref="CacheTtl"/> to reduce
/// database round-trips on hot paths (e.g. per-request authorization checks).
/// </summary>
public sealed class OrgMembershipQueryAdapter : IOrgMembershipQueryPort
{
    /// <summary>
    /// Time-to-live for the per-user org-membership cache entry.
    /// Must remain in the [30s, 60s] window specified in design.md §1.
    /// </summary>
    public static TimeSpan CacheTtl { get; } = TimeSpan.FromSeconds(45);

    private readonly IDbContextFactory<MarketplaceDbContext> _dbFactory;
    private readonly IMemoryCache _cache;

    public OrgMembershipQueryAdapter(
        IDbContextFactory<MarketplaceDbContext> dbFactory,
        IMemoryCache cache)
    {
        _dbFactory = dbFactory;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<Guid[]> GetOrgIdsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        string cacheKey = CacheKey(userId);

        if (_cache.TryGetValue(cacheKey, out Guid[]? cached) && cached is not null)
        {
            return cached;
        }

        await using MarketplaceDbContext ctx = await _dbFactory.CreateDbContextAsync(ct);

        Guid[] orgIds = await ctx.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.OrgId)
            .ToArrayAsync(ct);

        _cache.Set(cacheKey, orgIds, CacheTtl);

        return orgIds;
    }

    /// <inheritdoc />
    public async Task<bool> IsMemberAsync(
        Guid userId,
        Guid orgId,
        string? minRole = null,
        CancellationToken ct = default)
    {
        await using MarketplaceDbContext ctx = await _dbFactory.CreateDbContextAsync(ct);

        string? storedRole = await ctx.OrganizationMembers
            .Where(m => m.UserId == userId && m.OrgId == orgId)
            .Select(m => (string?)m.Role)
            .FirstOrDefaultAsync(ct);

        if (storedRole is null)
        {
            return false;
        }

        if (minRole is null)
        {
            return true;
        }

        return RoleLevel(storedRole) >= RoleLevel(minRole);
    }

    /// <summary>
    /// Removes the cached org-membership entry for the given user.
    /// Call this from any use-case that mutates org membership so that subsequent
    /// authorization checks reflect the updated state without waiting for TTL expiry.
    /// </summary>
    public void InvalidateUser(Guid userId)
    {
        _cache.Remove(CacheKey(userId));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string CacheKey(Guid userId) => $"org-membership:{userId}";

    /// <summary>
    /// Maps a role string to its numeric level for hierarchy comparisons.
    /// member=0, admin=1, owner=2. Unknown values map to -1 (never satisfies any minRole).
    /// </summary>
    private static int RoleLevel(string role) => role switch
    {
        "member" => 0,
        "admin"  => 1,
        "owner"  => 2,
        _        => -1,
    };
}
