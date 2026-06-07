using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Postgres-backed JWT denylist.
/// Implements <see cref="IRevokedJtiStorePort"/> using the <c>revoked_jti</c> table.
/// </summary>
public sealed class PostgresRevokedJtiStoreAdapter : IRevokedJtiStorePort
{
    private readonly MarketplaceDbContext _db;

    public PostgresRevokedJtiStoreAdapter(MarketplaceDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task AddAsync(string jti, DateTimeOffset tokenExpiresAt, CancellationToken ct = default)
    {
        // INSERT ... ON CONFLICT (jti) DO NOTHING — idempotent
        string sql = """
            INSERT INTO revoked_jti (jti, expires_at)
            VALUES ({0}, {1})
            ON CONFLICT (jti) DO NOTHING
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, [jti, tokenExpiresAt], ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        return await _db.RevokedJtis
            .AnyAsync(r => r.Jti == jti && r.ExpiresAt > DateTimeOffset.UtcNow, ct);
    }
}
