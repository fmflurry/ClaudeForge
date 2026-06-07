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
/// </summary>
public sealed class UserStoreAdapter : IUserStorePort
{
    private readonly IDbContextFactory<MarketplaceDbContext> _dbFactory;
    private readonly UserStoreOptions _options;

    public UserStoreAdapter(
        IDbContextFactory<MarketplaceDbContext> dbFactory,
        IOptions<UserStoreOptions> options)
    {
        _dbFactory = dbFactory;
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
        // Normalise email for case-insensitive lookup
        string emailNormalized = email.ToLowerInvariant();

        await using MarketplaceDbContext ctx = _dbFactory.CreateDbContext();

        // ── Rule 1/2: Existing (provider, subject) ────────────────────────────────
        UserIdentityEntity? existingIdentity = await ctx.UserIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Provider == provider && i.Subject == subject, ct)
            .ConfigureAwait(false);

        if (existingIdentity is not null)
        {
            // Update user email + displayName
            UserEntity? userToUpdate = await ctx.Users
                .FirstOrDefaultAsync(u => u.Id == existingIdentity.UserId, ct)
                .ConfigureAwait(false);

            if (userToUpdate is not null)
            {
                userToUpdate.Email = email;
                userToUpdate.EmailNormalized = emailNormalized;
                userToUpdate.DisplayName = displayName;
                userToUpdate.UpdatedAt = DateTimeOffset.UtcNow;
                await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

                return new ProvisionedUser(
                    userToUpdate.Id,
                    userToUpdate.Email,
                    userToUpdate.DisplayName,
                    IsNewUser: false);
            }
        }

        // ── Rule 3: Different provider, verified email, linking enabled ────────────
        if (emailVerified && !_options.DisableCrossProviderLinking)
        {
            UserEntity? existingUser = await ctx.Users
                .FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct)
                .ConfigureAwait(false);

            if (existingUser is not null)
            {
                // Add a new user_identity row linking this provider to the existing user
                UserIdentityEntity newIdentity = new()
                {
                    Id = Guid.NewGuid(),
                    UserId = existingUser.Id,
                    Provider = provider,
                    Subject = subject,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                ctx.UserIdentities.Add(newIdentity);

                try
                {
                    await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

                    return new ProvisionedUser(
                        existingUser.Id,
                        existingUser.Email,
                        existingUser.DisplayName,
                        IsNewUser: false);
                }
                catch (DbUpdateException)
                {
                    // Concurrent insert of same (provider, subject) — re-query winner
                    await ctx.Entry(newIdentity).ReloadAsync(ct).ConfigureAwait(false);
                    return await ResolveByProviderSubjectAsync(ctx, provider, subject, ct)
                        .ConfigureAwait(false);
                }
            }
        }

        // ── Rule 4 / first sign-in: Create new user + user_identity ───────────────
        return await CreateNewUserAsync(ctx, provider, subject, email, emailNormalized, displayName, ct)
            .ConfigureAwait(false);
    }

    private static async Task<ProvisionedUser> CreateNewUserAsync(
        MarketplaceDbContext ctx,
        string provider,
        string subject,
        string email,
        string emailNormalized,
        string displayName,
        CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        UserEntity newUser = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
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

            // Determine whether the constraint that fired was on user_identities or users.
            // If the (provider, subject) identity now exists in the DB, a concurrent first
            // sign-in race fired on user_identities — resolve the winner.
            // Otherwise the users.email_normalized unique constraint fired (the email is
            // already owned by a different user). This happens when a separate user must be
            // created for an unverified or cross-provider-linking-disabled sign-in, but the
            // email happens to be taken. Retry with a synthesized unique email_normalized so
            // the new user record can be stored while preserving the original display email.
            bool identityConflict = await ctx.UserIdentities
                .AsNoTracking()
                .AnyAsync(i => i.Provider == provider && i.Subject == subject, ct)
                .ConfigureAwait(false);

            if (identityConflict)
            {
                // Concurrent insert of same (provider, subject) — return the winner's data.
                return await ResolveByProviderSubjectAsync(ctx, provider, subject, ct)
                    .ConfigureAwait(false);
            }

            // Email uniqueness conflict: retry with a synthetic email_normalized that is
            // guaranteed unique per (provider, subject) so the new user row can be stored.
            string uniqueEmailNormalized = $"{provider}:{subject}".ToLowerInvariant();
            return await CreateNewUserAsync(
                ctx, provider, subject, email, uniqueEmailNormalized, displayName, ct)
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
