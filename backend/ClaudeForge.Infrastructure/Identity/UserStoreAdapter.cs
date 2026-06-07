using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudeForge.Infrastructure.Identity;

/// <summary>
/// Configuration options for <see cref="UserStoreAdapter"/>.
/// </summary>
public sealed class UserStoreOptions
{
    /// <summary>
    /// When true, cross-provider linking via verified email is disabled.
    /// Every new (provider, subject) pair creates an independent user account.
    /// Default: false.
    /// </summary>
    public bool DisableCrossProviderLinking { get; init; } = false;
}

/// <summary>
/// Provisions or links users using EF Core against the <see cref="MarketplaceDbContext"/>.
/// Implements the four linking rules from the design spec.
///
/// SECURITY: cross-provider linking requires email_verified on BOTH sides:
///   - the incoming identity must have emailVerified = true, AND
///   - the existing user must have email_normalized non-null (set from a verified email).
/// Users with unverified email are stored with email_normalized = NULL so they can NEVER
/// become link targets.
/// </summary>
public sealed class UserStoreAdapter : IUserStorePort
{
    private readonly MarketplaceDbContext _db;
    private readonly UserStoreOptions _options;

    public UserStoreAdapter(MarketplaceDbContext db, IOptions<UserStoreOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<ProvisionedUser> ProvisionOrLinkAsync(
        string provider,
        string subject,
        string email,
        bool emailVerified,
        string displayName,
        CancellationToken ct = default)
    {
        // Normalise email for case-insensitive lookup — only used when email is verified.
        string? emailNormalized = emailVerified ? email.ToLowerInvariant() : null;

        // ── Rule 1/2: Existing (provider, subject) ────────────────────────────────
        UserIdentityEntity? existingIdentity = await _db.UserIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Provider == provider && i.Subject == subject, ct)
            .ConfigureAwait(false);

        if (existingIdentity is not null)
        {
            UserEntity? userToUpdate = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == existingIdentity.UserId, ct)
                .ConfigureAwait(false);

            if (userToUpdate is not null)
            {
                userToUpdate.Email = email;
                // Only update email_normalized when email is verified; keep existing value otherwise.
                if (emailVerified)
                {
                    userToUpdate.EmailNormalized = emailNormalized;
                }

                userToUpdate.DisplayName = displayName;
                userToUpdate.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);

                return new ProvisionedUser(
                    userToUpdate.Id,
                    userToUpdate.Email,
                    userToUpdate.DisplayName,
                    IsNewUser: false);
            }
        }

        // ── Rule 3: Different provider, verified email, linking enabled ────────────
        // Cross-provider linking requires:
        //   1. Incoming email must be verified (emailNormalized is non-null).
        //   2. Existing user must have a non-null email_normalized (was set from a verified email).
        if (emailVerified && !_options.DisableCrossProviderLinking)
        {
            UserEntity? existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct)
                .ConfigureAwait(false);

            if (existingUser is not null)
            {
                UserIdentityEntity newIdentity = new()
                {
                    Id = Guid.NewGuid(),
                    UserId = existingUser.Id,
                    Provider = provider,
                    Subject = subject,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                _db.UserIdentities.Add(newIdentity);

                try
                {
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);

                    return new ProvisionedUser(
                        existingUser.Id,
                        existingUser.Email,
                        existingUser.DisplayName,
                        IsNewUser: false);
                }
                catch (DbUpdateException)
                {
                    _db.ChangeTracker.Clear();
                    // Concurrent insert of same (provider, subject) — re-query winner.
                    return await ResolveByProviderSubjectAsync(_db, provider, subject, ct)
                        .ConfigureAwait(false);
                }
            }
        }

        // ── Rule 4 / first sign-in: Create new user + user_identity ───────────────
        return await CreateNewUserAsync(_db, provider, subject, email, emailNormalized, displayName, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UserProfile?> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                Memberships = u.Memberships
                    .Select(m => new
                    {
                        m.OrgId,
                        OrgName = m.Organization.Name,
                        m.Role,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (user is null)
        {
            return null;
        }

        IReadOnlyList<UserOrgMembership> memberships = user.Memberships
            .Select(m => new UserOrgMembership(m.OrgId, m.OrgName, m.Role))
            .ToList();

        return new UserProfile(user.Id, user.Email, user.DisplayName, memberships);
    }

    private static async Task<ProvisionedUser> CreateNewUserAsync(
        MarketplaceDbContext ctx,
        string provider,
        string subject,
        string email,
        string? emailNormalized,
        string displayName,
        CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserEntity newUser = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            // NULL for unverified — can never be a cross-provider link target.
            EmailNormalized = emailNormalized,
            DisplayName = displayName,
            CreatedAt = now,
            UpdatedAt = now,
        };

        UserIdentityEntity newIdentity = new()
        {
            Id = Guid.NewGuid(),
            UserId = newUser.Id,
            Provider = provider,
            Subject = subject,
            CreatedAt = now,
        };

        ctx.Users.Add(newUser);
        ctx.UserIdentities.Add(newIdentity);

        try
        {
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

            return new ProvisionedUser(
                newUser.Id,
                newUser.Email,
                newUser.DisplayName,
                IsNewUser: true);
        }
        catch (DbUpdateException)
        {
            ctx.ChangeTracker.Clear();

            // Determine whether the identity constraint fired (concurrent first sign-in).
            bool identityConflict = await ctx.UserIdentities
                .AsNoTracking()
                .AnyAsync(i => i.Provider == provider && i.Subject == subject, ct)
                .ConfigureAwait(false);

            if (identityConflict)
            {
                return await ResolveByProviderSubjectAsync(ctx, provider, subject, ct)
                    .ConfigureAwait(false);
            }

            // Email uniqueness conflict on email_normalized: create user without email_normalized
            // so the row can always be stored without colliding with existing verified users.
            return await CreateNewUserAsync(
                ctx, provider, subject, email, emailNormalized: null, displayName, ct)
                .ConfigureAwait(false);
        }
    }

    private static async Task<ProvisionedUser> ResolveByProviderSubjectAsync(
        MarketplaceDbContext ctx,
        string provider,
        string subject,
        CancellationToken ct)
    {
        UserIdentityEntity identity = await ctx.UserIdentities
            .AsNoTracking()
            .Include(i => i.User)
            .FirstAsync(i => i.Provider == provider && i.Subject == subject, ct)
            .ConfigureAwait(false);

        return new ProvisionedUser(
            identity.UserId,
            identity.User.Email,
            identity.User.DisplayName,
            IsNewUser: false);
    }
}
