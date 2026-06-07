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

        RefreshTokenEntity entity = new()
        {
            Id = Guid.NewGuid(),
            UserId = cmd.UserId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
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
    public async Task<RefreshTokenEntity?> FindByHashAsync(
        string plainToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return null;
        }

        string hash = HashToken(plainToken);

        return await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
    }

    /// <inheritdoc />
    public async Task<RotateRefreshTokenResult> RotateAsync(
        Guid oldId,
        Guid userId,
        CancellationToken ct = default)
    {
        RefreshTokenEntity? oldEntity = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Id == oldId, ct)
            ?? throw new InvalidOperationException($"Refresh token {oldId} not found.");

        string newPlainToken = GeneratePlainToken();
        string newTokenHash = HashToken(newPlainToken);
        Guid newId = Guid.NewGuid();
        DateTimeOffset newExpiresAt = DateTimeOffset.UtcNow.AddDays(_defaultExpiryDays);

        RefreshTokenEntity newEntity = new()
        {
            Id = newId,
            UserId = userId,
            TokenHash = newTokenHash,
            ExpiresAt = newExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Mark old token as rotated (immutable update: set rotated_to)
        oldEntity.RotatedTo = newId;

        _db.RefreshTokens.Add(newEntity);
        await _db.SaveChangesAsync(ct);

        return new RotateRefreshTokenResult(
            NewId: newId,
            NewPlainToken: newPlainToken,
            NewExpiresAt: newExpiresAt);
    }

    /// <inheritdoc />
    public async Task RevokeChainAsync(Guid rootId, CancellationToken ct = default)
    {
        // Walk the rotated_to chain, revoking each node
        Guid? currentId = rootId;
        DateTimeOffset revokedAt = DateTimeOffset.UtcNow;

        while (currentId.HasValue)
        {
            RefreshTokenEntity? entity = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Id == currentId.Value, ct);

            if (entity is null)
            {
                break;
            }

            entity.RevokedAt = revokedAt;
            Guid? nextId = entity.RotatedTo;
            await _db.SaveChangesAsync(ct);

            currentId = nextId;
        }
    }

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
