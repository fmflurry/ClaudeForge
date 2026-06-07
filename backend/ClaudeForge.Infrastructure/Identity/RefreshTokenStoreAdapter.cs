using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Postgres-backed refresh-token store.
/// Implements <see cref="IRefreshTokenStorePort"/> with opaque random tokens
/// stored as SHA-256 hex digests (never plaintext).
/// Rotation is atomic: a conditional UPDATE (rotated_to IS NULL AND revoked_at IS NULL)
/// ensures only one caller can rotate a given token.
/// Revocation operates on the whole family via root_id.
/// </summary>
public sealed class RefreshTokenStoreAdapter : IRefreshTokenStorePort
{
    private readonly MarketplaceDbContext _db;
    private readonly int _defaultExpiryDays;

    public RefreshTokenStoreAdapter(MarketplaceDbContext db, int defaultExpiryDays = 30)
    {
        _db = db;
        _defaultExpiryDays = defaultExpiryDays;
    }

    /// <inheritdoc />
    public async Task<RefreshTokenResult> CreateAsync(
        CreateRefreshTokenCommand cmd,
        CancellationToken ct = default)
    {
        int expiryDays = cmd.ExpiryDays != 0 ? cmd.ExpiryDays : _defaultExpiryDays;

        string plainToken = GeneratePlainToken();
        string tokenHash = HashToken(plainToken);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(expiryDays);
        Guid newId = Guid.NewGuid();

        RefreshTokenEntity entity = new()
        {
            Id = newId,
            UserId = cmd.UserId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            // Family root = the row's own Id for new tokens.
            RootId = newId,
            Provider = cmd.Provider,
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new RefreshTokenResult(
            Id: entity.Id,
            UserId: entity.UserId,
            PlainToken: plainToken,
            ExpiresAt: expiresAt);
    }

    /// <inheritdoc />
    public async Task<RefreshTokenInfo?> FindByHashAsync(
        string plainToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return null;
        }

        string hash = HashToken(plainToken);

        RefreshTokenEntity? entity = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        return entity is null ? null : MapToInfo(entity);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Atomic rotation with race/reuse safety:
    ///   1. Fetch the old entity to check its provider and ensure it exists.
    ///   2. Insert the new token row first (so the FK reference from rotated_to is valid).
    ///   3. Conditionally UPDATE the old row — sets rotated_to only when
    ///      rotated_to IS NULL AND revoked_at IS NULL.
    ///   4. If 0 rows are affected → the token was already rotated or revoked (reuse/race) →
    ///      clean up the newly inserted row and throw.
    /// </remarks>
    public async Task<RotateRefreshTokenResult> RotateAsync(
        Guid oldId,
        Guid userId,
        Guid rootId,
        CancellationToken ct = default)
    {
        string newPlainToken = GeneratePlainToken();
        string newTokenHash = HashToken(newPlainToken);
        Guid newId = Guid.NewGuid();
        DateTimeOffset newExpiresAt = DateTimeOffset.UtcNow.AddDays(_defaultExpiryDays);

        // Fetch the old entity to get its provider and verify it exists.
        RefreshTokenEntity? oldEntity = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Id == oldId, ct)
            ?? throw new InvalidOperationException($"Refresh token {oldId} not found.");

        // Insert the new token row first (FK requires the referent to exist before rotated_to is set).
        RefreshTokenEntity newEntity = new()
        {
            Id = newId,
            UserId = userId,
            TokenHash = newTokenHash,
            ExpiresAt = newExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            // Inherit the family root from the parent.
            RootId = rootId,
            Provider = oldEntity.Provider,
        };

        _db.RefreshTokens.Add(newEntity);
        await _db.SaveChangesAsync(ct);

        // Atomic conditional update: only set rotated_to when still unused.
        int rowsAffected = await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE refresh_tokens
            SET rotated_to = {0}
            WHERE id = {1}
              AND rotated_to IS NULL
              AND revoked_at IS NULL
            """,
            newId,
            oldId);

        if (rowsAffected == 0)
        {
            // Reuse or race condition detected — remove the newly inserted token and reject.
            // The caller is responsible for revoking the family and returning 401.
            _db.RefreshTokens.Remove(newEntity);
            await _db.SaveChangesAsync(ct);

            throw new AuthenticationException(
                "Refresh token reuse detected. The token family has been revoked.");
        }

        return new RotateRefreshTokenResult(
            NewId: newId,
            NewPlainToken: newPlainToken,
            NewExpiresAt: newExpiresAt);
    }

    /// <inheritdoc />
    public async Task RevokeChainAsync(Guid rootId, CancellationToken ct = default)
    {
        // Revoke the entire family in a single statement using root_id.
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE refresh_tokens
            SET revoked_at = NOW()
            WHERE root_id = {0}
              AND revoked_at IS NULL
            """,
            rootId);
    }

    private static RefreshTokenInfo MapToInfo(RefreshTokenEntity entity) =>
        new(
            Id: entity.Id,
            UserId: entity.UserId,
            ExpiresAt: entity.ExpiresAt,
            RevokedAt: entity.RevokedAt,
            RotatedTo: entity.RotatedTo,
            RootId: entity.RootId,
            Provider: entity.Provider);

    private static string GeneratePlainToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string plainToken)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(plainToken);
        byte[] hash = SHA256.HashData(inputBytes);
        return Convert.ToHexStringLower(hash);
    }
}
