using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Infrastructure adapter for GDPR-compliant user account deletion operations.
/// Each method targets one aspect of the deletion workflow and executes a
/// single SQL statement via EF Core's ExecuteSqlRawAsync.
/// </summary>
public sealed class UserDeletionAdapter : IUserDeletionPort
{
    private readonly MarketplaceDbContext _db;

    public UserDeletionAdapter(MarketplaceDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task SoftDeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE users SET deleted_at = NOW() WHERE id = {0}",
            [userId],
            ct);
    }

    /// <inheritdoc />
    public async Task RemoveAllMembershipsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM organization_members WHERE user_id = {0}",
            [userId],
            ct);
    }

    /// <inheritdoc />
    public async Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE refresh_tokens SET revoked_at = NOW() WHERE user_id = {0} AND revoked_at IS NULL",
            [userId],
            ct);
    }
}
