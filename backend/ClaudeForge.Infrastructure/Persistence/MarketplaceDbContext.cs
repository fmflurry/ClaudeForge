using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NpgsqlTypes;

namespace ClaudeForge.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the plugin marketplace.
/// Owns all tables: plugins, plugin_versions, categories, plugin_categories,
/// telemetry_events, telemetry_aggregates, doc_pages, users, user_identities,
/// organizations, organization_members, organization_invitations, refresh_tokens,
/// and org_audit_log.
/// </summary>
public sealed class MarketplaceDbContext : DbContext
{
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    // Existing sets
    public DbSet<PluginEntity> Plugins => Set<PluginEntity>();
    public DbSet<PluginVersionEntity> PluginVersions => Set<PluginVersionEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<PluginCategoryEntity> PluginCategories => Set<PluginCategoryEntity>();
    public DbSet<TelemetryEventEntity> TelemetryEvents => Set<TelemetryEventEntity>();
    public DbSet<TelemetryAggregateEntity> TelemetryAggregates => Set<TelemetryAggregateEntity>();
    public DbSet<DocPageEntity> DocPages => Set<DocPageEntity>();

    // Auth sets
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserIdentityEntity> UserIdentities => Set<UserIdentityEntity>();
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<OrganizationMemberEntity> OrganizationMembers => Set<OrganizationMemberEntity>();
    public DbSet<OrganizationInvitationEntity> OrganizationInvitations => Set<OrganizationInvitationEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<OrgAuditEntryEntity> OrgAuditLog => Set<OrgAuditEntryEntity>();
    public DbSet<RevokedJtiEntity> RevokedJtis => Set<RevokedJtiEntity>();

    // Security analysis sets
    public DbSet<AnalysisJobEntity> AnalysisJobs => Set<AnalysisJobEntity>();
    public DbSet<AnalysisResultEntity> AnalysisResults => Set<AnalysisResultEntity>();
    public DbSet<AnalysisConfigEntity> AnalysisConfig => Set<AnalysisConfigEntity>();
    public DbSet<ConfigChangeLogEntity> ConfigChangeLogs => Set<ConfigChangeLogEntity>();
    public DbSet<AppealEntity> Appeals => Set<AppealEntity>();

    // Safe zone / org block sets
    public DbSet<SafeZonePluginEntity> SafeZonePlugins => Set<SafeZonePluginEntity>();
    public DbSet<OrgPluginBlockEntity> OrgPluginBlocks => Set<OrgPluginBlockEntity>();

    // Reputation sets
    public DbSet<BadgeEntity> Badges => Set<BadgeEntity>();
    public DbSet<AuthorBadgeEntity> AuthorBadges => Set<AuthorBadgeEntity>();
    public DbSet<AuthorReputationEntity> AuthorReputations => Set<AuthorReputationEntity>();
    public DbSet<KarmaEventEntity> KarmaEvents => Set<KarmaEventEntity>();
    public DbSet<LeaderboardCacheEntity> LeaderboardCache => Set<LeaderboardCacheEntity>();

    // Notification sets
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<UserNotificationPreferencesEntity> UserNotificationPreferences => Set<UserNotificationPreferencesEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // The NpgsqlTsVector value converter is only valid for the PostgreSQL provider.
        // When running against the in-memory provider (unit tests) the converter must
        // be skipped to avoid a model-validation failure on the tsvector columns.
        bool isPostgres = Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) ?? false;

        ConfigurePlugins(modelBuilder, isPostgres);
        ConfigurePluginVersions(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigurePluginCategories(modelBuilder);
        ConfigureTelemetryEvents(modelBuilder);
        ConfigureTelemetryAggregates(modelBuilder);
        ConfigureDocPages(modelBuilder, isPostgres);
        ConfigureUsers(modelBuilder);
        ConfigureUserIdentities(modelBuilder);
        ConfigureOrganizations(modelBuilder);
        ConfigureOrganizationMembers(modelBuilder);
        ConfigureOrganizationInvitations(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureOrgAuditLog(modelBuilder);
        ConfigureRevokedJti(modelBuilder);
        ConfigureAnalysisJobs(modelBuilder);
        ConfigureAnalysisResults(modelBuilder);
        ConfigureAnalysisConfig(modelBuilder);
        ConfigureConfigChangeLog(modelBuilder);
        ConfigureAppeals(modelBuilder);
        ConfigureSafeZonePlugins(modelBuilder);
        ConfigureOrgPluginBlocks(modelBuilder);
        ConfigureBadges(modelBuilder);
        ConfigureAuthorBadges(modelBuilder);
        ConfigureAuthorReputations(modelBuilder);
        ConfigureKarmaEvents(modelBuilder);
        ConfigureLeaderboardCache(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ConfigureUserNotificationPreferences(modelBuilder);
    }

    private static void ConfigurePlugins(ModelBuilder modelBuilder, bool isPostgres = true)
    {
        modelBuilder.Entity<PluginEntity>(entity =>
        {
            entity.ToTable("plugins");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(p => p.Name)
                  .HasColumnName("name")
                  .IsRequired();

            entity.Property(p => p.NameNormalized)
                  .HasColumnName("name_normalized")
                  .IsRequired();

            entity.Property(p => p.Slug)
                  .HasColumnName("slug")
                  .IsRequired();

            entity.Property(p => p.Description)
                  .HasColumnName("description")
                  .IsRequired();

            entity.Property(p => p.Author)
                  .HasColumnName("author")
                  .IsRequired();

            entity.Property(p => p.DownloadCount)
                  .HasColumnName("download_count")
                  .HasDefaultValue(0L);

            // search_vector is a PostgreSQL GENERATED ALWAYS AS STORED tsvector column.
            // The DB computes it automatically on every INSERT/UPDATE; EF never writes it.
            // The entity property is string? so that callers can use standard string assertions.
            // A ValueConverter bridges string? ↔ NpgsqlTsVector? for the Npgsql provider.
            // ValueComparer is required to avoid a NullReferenceException in the EF Core 9.x
            // MigrationsModelDiffer when generating migrations (ProviderValueComparer NPE).
            // The converter is skipped for the in-memory provider (unit tests) because
            // NpgsqlTsVector is not supported by that provider.
            if (isPostgres)
            {
                // NpgsqlTsVector.Parse is marked obsolete for runtime use, but this converter
                // is a CLR materialization shim for EF Core's model configuration — the column
                // is a server-computed "tsvector" type and Parse is only called when EF Core
                // needs a CLR value for change tracking, not in the hot path.
#pragma warning disable CS0618 // NpgsqlTsVector.Parse: acceptable in EF value-converter shim
                ValueConverter<string?, NpgsqlTsVector?> tsVectorConverter = new(
                    v => v == null ? null : NpgsqlTsVector.Parse(v),
                    v => v == null ? null : v.ToString());
#pragma warning restore CS0618
                ValueComparer<string?> tsVectorComparer = new(
                    (a, b) => a == b,
                    v => v == null ? 0 : v.GetHashCode(),
                    v => v);

                entity.Property(p => p.SearchVector)
                      .HasColumnName("search_vector")
                      .HasColumnType("tsvector")
                      .HasConversion(tsVectorConverter, tsVectorComparer)
                      .HasComputedColumnSql(
                          "to_tsvector('english', coalesce(name,'') || ' ' || coalesce(description,''))",
                          stored: true)
                      .IsRequired(false);
            }
            else
            {
                entity.Property(p => p.SearchVector).IsRequired(false);
            }

            entity.Property(p => p.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            // Unique constraints
            entity.HasIndex(p => p.NameNormalized)
                  .IsUnique()
                  .HasDatabaseName("ix_plugins_name_normalized");

            entity.HasIndex(p => p.Slug)
                  .IsUnique()
                  .HasDatabaseName("ix_plugins_slug");

            // NOTE: The GIN index on search_vector is created via raw SQL in the migration
            // (idx_plugins_search_vector) because EF cannot configure a GIN index on a
            // tsvector GENERATED column with a standard HasIndex call.

            // Additive auth columns (task 1.5)
            entity.Property(p => p.Visibility)
                  .HasColumnName("visibility")
                  .HasDefaultValue("public")
                  .IsRequired();

            entity.Property(p => p.OwnerOrgId)
                  .HasColumnName("owner_org_id")
                  .IsRequired(false);

            entity.Property(p => p.OwnerUserId)
                  .HasColumnName("owner_user_id")
                  .IsRequired(false);

            // FK → organizations (no cascade — plugins outlive org removal for audit purposes)
            entity.HasOne<OrganizationEntity>()
                  .WithMany()
                  .HasForeignKey(p => p.OwnerOrgId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            // FK → users (no cascade — plugins outlive user removal)
            entity.HasOne<UserEntity>()
                  .WithMany()
                  .HasForeignKey(p => p.OwnerUserId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            // CHECK: visibility='public' OR owner_org_id IS NOT NULL
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "chk_visibility_owner",
                    "visibility = 'public' OR owner_org_id IS NOT NULL");
                t.HasCheckConstraint(
                    "chk_plugins_security_status",
                    "security_status IN ('pending', 'passed', 'failed', 'in_review')");
            });

            // Composite index for visibility + owner_org_id filter queries
            entity.HasIndex(p => new { p.Visibility, p.OwnerOrgId })
                  .HasDatabaseName("idx_plugins_visibility_org");

            entity.Property(p => p.IsFeatured)
                  .HasColumnName("is_featured")
                  .HasDefaultValue(false);

            // Partial unique index — at most one featured plugin at a time
            entity.HasIndex(p => p.IsFeatured)
                  .IsUnique()
                  .HasFilter("is_featured = true")
                  .HasDatabaseName("ux_plugins_featured");

            entity.Property(p => p.SecurityScore)
                  .HasColumnName("security_score")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(0m)
                  .IsRequired();

            entity.Property(p => p.SecurityStatus)
                  .HasColumnName("security_status")
                  .HasDefaultValue("pending")
                  .IsRequired();
        });
    }

    private static void ConfigurePluginVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PluginVersionEntity>(entity =>
        {
            entity.ToTable("plugin_versions");
            entity.HasKey(pv => pv.Id);
            entity.Property(pv => pv.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(pv => pv.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired();

            entity.Property(pv => pv.Version)
                  .HasColumnName("version")
                  .IsRequired();

            entity.Property(pv => pv.VersionSort)
                  .HasColumnName("version_sort")
                  .IsRequired();

            entity.Property(pv => pv.ReleaseNotes)
                  .HasColumnName("release_notes")
                  .HasDefaultValue(string.Empty);

            entity.Property(pv => pv.IsLatest)
                  .HasColumnName("is_latest")
                  .HasDefaultValue(false);

            entity.Property(pv => pv.PackageKey)
                  .HasColumnName("package_key")
                  .IsRequired();

            entity.Property(pv => pv.PackageFormat)
                  .HasColumnName("package_format")
                  .IsRequired();

            entity.Property(pv => pv.SizeBytes)
                  .HasColumnName("size_bytes")
                  .IsRequired();

            entity.Property(pv => pv.Sha256)
                  .HasColumnName("sha256")
                  .IsRequired();

            entity.Property(pv => pv.DownloadCount)
                  .HasColumnName("download_count")
                  .HasDefaultValue(0L);

            entity.Property(pv => pv.ReadmeText)
                  .HasColumnName("readme_text")
                  .IsRequired(false);

            entity.Property(pv => pv.ReleasedAt)
                  .HasColumnName("released_at")
                  .HasDefaultValueSql("NOW()");

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(pv => pv.Plugin)
                  .WithMany(p => p.Versions)
                  .HasForeignKey(pv => pv.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            // UNIQUE(plugin_id, version)
            entity.HasIndex(pv => new { pv.PluginId, pv.Version })
                  .IsUnique()
                  .HasDatabaseName("ix_plugin_versions_plugin_version");

            // Partial UNIQUE: only one is_latest per plugin
            entity.HasIndex(pv => pv.PluginId)
                  .IsUnique()
                  .HasFilter("is_latest = TRUE")
                  .HasDatabaseName("ix_plugin_versions_single_latest");

            // Performance index: version_sort descending per plugin
            entity.HasIndex(pv => new { pv.PluginId, pv.VersionSort })
                  .HasDatabaseName("idx_plugin_versions_sort");
        });
    }

    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id)
                  .HasColumnName("id")
                  .UseIdentityByDefaultColumn();

            entity.Property(c => c.Dimension)
                  .HasColumnName("dimension")
                  .IsRequired();

            entity.Property(c => c.Value)
                  .HasColumnName("value")
                  .IsRequired();

            entity.Property(c => c.DisplayName)
                  .HasColumnName("display_name")
                  .IsRequired(false);

            entity.Property(c => c.Description)
                  .HasColumnName("description")
                  .IsRequired(false);

            // UNIQUE(dimension, value)
            entity.HasIndex(c => new { c.Dimension, c.Value })
                  .IsUnique()
                  .HasDatabaseName("ix_categories_dimension_value");
        });
    }

    private static void ConfigurePluginCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PluginCategoryEntity>(entity =>
        {
            entity.ToTable("plugin_categories");

            // Composite PK
            entity.HasKey(pc => new { pc.PluginId, pc.CategoryId });

            entity.Property(pc => pc.PluginId).HasColumnName("plugin_id");
            entity.Property(pc => pc.CategoryId).HasColumnName("category_id");

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(pc => pc.Plugin)
                  .WithMany(p => p.PluginCategories)
                  .HasForeignKey(pc => pc.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → categories (no cascade — deleting a category is intentional admin action)
            entity.HasOne(pc => pc.Category)
                  .WithMany(c => c.PluginCategories)
                  .HasForeignKey(pc => pc.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTelemetryEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryEventEntity>(entity =>
        {
            entity.ToTable("telemetry_events");
            entity.HasKey(te => te.Id);
            entity.Property(te => te.Id)
                  .HasColumnName("id")
                  .UseIdentityByDefaultColumn();

            entity.Property(te => te.EventType)
                  .HasColumnName("event_type")
                  .IsRequired();

            // Nullable FK → plugins ON DELETE SET NULL
            entity.Property(te => te.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired(false);

            entity.HasOne<PluginEntity>()
                  .WithMany()
                  .HasForeignKey(te => te.PluginId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            entity.Property(te => te.Version)
                  .HasColumnName("version")
                  .IsRequired(false);

            // CHAR(64) — SHA-256 hex of UUID v4 client identifier
            entity.Property(te => te.AnonClientId)
                  .HasColumnName("anon_client_id")
                  .HasColumnType("char(64)")
                  .IsRequired(false);

            entity.Property(te => te.ClientOs)
                  .HasColumnName("client_os")
                  .IsRequired(false);

            entity.Property(te => te.ClientArch)
                  .HasColumnName("client_arch")
                  .IsRequired(false);

            entity.Property(te => te.OccurredAt)
                  .HasColumnName("occurred_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasIndex(te => te.OccurredAt)
                  .HasDatabaseName("idx_telemetry_events_ts");

            entity.HasIndex(te => te.PluginId)
                  .HasDatabaseName("idx_telemetry_events_plugin");
        });
    }

    private static void ConfigureTelemetryAggregates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryAggregateEntity>(entity =>
        {
            entity.ToTable("telemetry_aggregates");

            // Composite PK
            entity.HasKey(ta => new { ta.PluginId, ta.Version, ta.EventType, ta.WindowStart });

            entity.Property(ta => ta.PluginId).HasColumnName("plugin_id");
            entity.Property(ta => ta.Version).HasColumnName("version");
            entity.Property(ta => ta.EventType).HasColumnName("event_type");

            entity.Property(ta => ta.Count)
                  .HasColumnName("count")
                  .HasDefaultValue(0L);

            entity.Property(ta => ta.WindowStart)
                  .HasColumnName("window_start");

            // FK → plugins (no cascade annotation; aggregate data kept after plugin removal)
            entity.HasOne<PluginEntity>()
                  .WithMany()
                  .HasForeignKey(ta => ta.PluginId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(u => u.Email)
                  .HasColumnName("email")
                  .IsRequired();

            entity.Property(u => u.EmailNormalized)
                  .HasColumnName("email_normalized")
                  .IsRequired(false);

            entity.Property(u => u.DisplayName)
                  .HasColumnName("display_name")
                  .IsRequired();

            entity.Property(u => u.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(u => u.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(u => u.DeletedAt)
                  .HasColumnName("deleted_at")
                  .IsRequired(false);

            // UNIQUE on email_normalized
            entity.HasIndex(u => u.EmailNormalized)
                  .IsUnique()
                  .HasDatabaseName("ix_users_email_normalized");
        });
    }

    private static void ConfigureUserIdentities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserIdentityEntity>(entity =>
        {
            entity.ToTable("user_identities");
            entity.HasKey(ui => ui.Id);
            entity.Property(ui => ui.Id)
                  .HasColumnName("id");

            entity.Property(ui => ui.UserId)
                  .HasColumnName("user_id")
                  .IsRequired();

            entity.Property(ui => ui.Provider)
                  .HasColumnName("provider")
                  .IsRequired();

            entity.Property(ui => ui.Subject)
                  .HasColumnName("subject")
                  .IsRequired();

            entity.Property(ui => ui.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → users ON DELETE CASCADE
            entity.HasOne(ui => ui.User)
                  .WithMany(u => u.Identities)
                  .HasForeignKey(ui => ui.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // UNIQUE(provider, subject)
            entity.HasIndex(ui => new { ui.Provider, ui.Subject })
                  .IsUnique()
                  .HasDatabaseName("ix_user_identities_provider_subject");
        });
    }

    private static void ConfigureOrganizations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id)
                  .HasColumnName("id");

            entity.Property(o => o.Name)
                  .HasColumnName("name")
                  .IsRequired();

            entity.Property(o => o.NameNormalized)
                  .HasColumnName("name_normalized")
                  .IsRequired();

            entity.Property(o => o.Slug)
                  .HasColumnName("slug")
                  .IsRequired();

            entity.Property(o => o.CreatedBy)
                  .HasColumnName("created_by")
                  .IsRequired();

            entity.Property(o => o.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → users (no cascade — org persists even if creator is soft-deleted)
            entity.HasOne<UserEntity>()
                  .WithMany()
                  .HasForeignKey(o => o.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            // UNIQUE on name_normalized
            entity.HasIndex(o => o.NameNormalized)
                  .IsUnique()
                  .HasDatabaseName("ix_organizations_name_normalized");

            // UNIQUE on slug
            entity.HasIndex(o => o.Slug)
                  .IsUnique()
                  .HasDatabaseName("ix_organizations_slug");
        });
    }

    private static void ConfigureOrganizationMembers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationMemberEntity>(entity =>
        {
            entity.ToTable("organization_members");

            // Surrogate PK — EF tracks by Id; the DB semantic uniqueness is
            // enforced by the UNIQUE(org_id, user_id) index below.
            // This prevents EF's identity-map from throwing when two entity
            // instances with the same (OrgId, UserId) are added to the same
            // DbContext before SaveChangesAsync is called, which would cause
            // an InvalidOperationException rather than a DbUpdateException.
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(m => m.OrgId).HasColumnName("org_id");
            entity.Property(m => m.UserId).HasColumnName("user_id");

            entity.Property(m => m.Role)
                  .HasColumnName("role")
                  .IsRequired();

            entity.Property(m => m.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → organizations ON DELETE CASCADE
            entity.HasOne(m => m.Organization)
                  .WithMany(o => o.Members)
                  .HasForeignKey(m => m.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → users ON DELETE CASCADE
            entity.HasOne(m => m.User)
                  .WithMany(u => u.Memberships)
                  .HasForeignKey(m => m.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Semantic UNIQUE constraint: one membership per (org, user)
            entity.HasIndex(m => new { m.OrgId, m.UserId })
                  .IsUnique()
                  .HasDatabaseName("ix_organization_members_org_user");
        });
    }

    private static void ConfigureOrganizationInvitations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationInvitationEntity>(entity =>
        {
            entity.ToTable("organization_invitations");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id)
                  .HasColumnName("id");

            entity.Property(i => i.OrgId)
                  .HasColumnName("org_id")
                  .IsRequired();

            entity.Property(i => i.EmailNormalized)
                  .HasColumnName("email_normalized")
                  .IsRequired();

            entity.Property(i => i.InvitedBy)
                  .HasColumnName("invited_by")
                  .IsRequired();

            entity.Property(i => i.Role)
                  .HasColumnName("role")
                  .HasDefaultValue("member")
                  .IsRequired();

            entity.Property(i => i.Status)
                  .HasColumnName("status")
                  .HasDefaultValue("pending")
                  .IsRequired();

            entity.Property(i => i.Token)
                  .HasColumnName("token")
                  .IsRequired();

            entity.Property(i => i.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(i => i.ExpiresAt)
                  .HasColumnName("expires_at")
                  .IsRequired();

            entity.Property(i => i.AcceptedAt)
                  .HasColumnName("accepted_at")
                  .IsRequired(false);

            entity.Property(i => i.RevokedAt)
                  .HasColumnName("revoked_at")
                  .IsRequired(false);

            // FK → organizations ON DELETE CASCADE
            entity.HasOne(i => i.Organization)
                  .WithMany(o => o.Invitations)
                  .HasForeignKey(i => i.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → users (invited_by; no cascade — invitation record preserved)
            entity.HasOne(i => i.InvitedByUser)
                  .WithMany(u => u.SentInvitations)
                  .HasForeignKey(i => i.InvitedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            // UNIQUE on token
            entity.HasIndex(i => i.Token)
                  .IsUnique()
                  .HasDatabaseName("ix_organization_invitations_token");

            // Partial UNIQUE(org_id, email_normalized) WHERE status='pending'
            entity.HasIndex(i => new { i.OrgId, i.EmailNormalized })
                  .IsUnique()
                  .HasFilter("status = 'pending'")
                  .HasDatabaseName("ix_organization_invitations_pending_org_email");
        });
    }

    private static void ConfigureRefreshTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id)
                  .HasColumnName("id");

            entity.Property(r => r.UserId)
                  .HasColumnName("user_id")
                  .IsRequired();

            // CHAR(64) — SHA-256 hex digest
            entity.Property(r => r.TokenHash)
                  .HasColumnName("token_hash")
                  .HasColumnType("char(64)")
                  .IsRequired();

            entity.Property(r => r.ExpiresAt)
                  .HasColumnName("expires_at")
                  .IsRequired();

            entity.Property(r => r.RevokedAt)
                  .HasColumnName("revoked_at")
                  .IsRequired(false);

            entity.Property(r => r.RotatedTo)
                  .HasColumnName("rotated_to")
                  .IsRequired(false);

            entity.Property(r => r.RootId)
                  .HasColumnName("root_id")
                  .IsRequired();

            entity.Property(r => r.Provider)
                  .HasColumnName("provider")
                  .IsRequired();

            entity.Property(r => r.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → users ON DELETE CASCADE
            entity.HasOne(r => r.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Self-FK → refresh_tokens (rotated_to)
            entity.HasOne(r => r.RotatedToToken)
                  .WithMany()
                  .HasForeignKey(r => r.RotatedTo)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);

            // UNIQUE on token_hash
            entity.HasIndex(r => r.TokenHash)
                  .IsUnique()
                  .HasDatabaseName("ix_refresh_tokens_token_hash");

            // Index on root_id for efficient family-wide revocation.
            entity.HasIndex(r => r.RootId)
                  .HasDatabaseName("ix_refresh_tokens_root_id");

            // Enforce exactly 64 non-space characters.
            // PostgreSQL char(n) pads shorter values with spaces rather than rejecting them,
            // so a CHECK constraint is needed to enforce the SHA-256 hex length invariant.
            entity.ToTable(t => t.HasCheckConstraint(
                "chk_refresh_tokens_token_hash_length",
                "char_length(trim(token_hash)) = 64"));
        });
    }

    private static void ConfigureOrgAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgAuditEntryEntity>(entity =>
        {
            entity.ToTable("org_audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .HasColumnName("id");

            entity.Property(e => e.OrgId)
                  .HasColumnName("org_id")
                  .IsRequired();

            entity.Property(e => e.ActorUserId)
                  .HasColumnName("actor_user_id")
                  .IsRequired();

            entity.Property(e => e.Action)
                  .HasColumnName("action")
                  .IsRequired();

            entity.Property(e => e.Target)
                  .HasColumnName("target")
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → organizations ON DELETE CASCADE
            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.AuditLog)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → users (no cascade — audit record preserved even if actor is deleted)
            entity.HasOne(e => e.ActorUser)
                  .WithMany()
                  .HasForeignKey(e => e.ActorUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDocPages(ModelBuilder modelBuilder, bool isPostgres = true)
    {
        modelBuilder.Entity<DocPageEntity>(entity =>
        {
            entity.ToTable("doc_pages");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(d => d.Slug)
                  .HasColumnName("slug")
                  .IsRequired();

            entity.Property(d => d.Title)
                  .HasColumnName("title")
                  .IsRequired();

            entity.Property(d => d.ContentMarkdown)
                  .HasColumnName("content_markdown")
                  .IsRequired();

            entity.Property(d => d.Category)
                  .HasColumnName("category")
                  .IsRequired();

            entity.Property(d => d.LastUpdated)
                  .HasColumnName("last_updated")
                  .HasDefaultValueSql("NOW()");

            // search_vector is a PostgreSQL GENERATED ALWAYS AS STORED tsvector column.
            // title is weighted A (higher relevance), content_markdown is weighted B (lower).
            // The DB computes it automatically on every INSERT/UPDATE; EF never writes it.
            // A ValueConverter bridges string? ↔ NpgsqlTsVector? for the Npgsql provider.
            // ValueComparer is required to avoid a NullReferenceException in the EF Core 9.x
            // MigrationsModelDiffer when generating migrations (ProviderValueComparer NPE).
            // The converter is skipped for the in-memory provider (unit tests) because
            // NpgsqlTsVector is not supported by that provider.
            if (isPostgres)
            {
                // NpgsqlTsVector.Parse is marked obsolete for runtime use, but this converter
                // is a CLR materialization shim for EF Core's model configuration — the column
                // is a server-computed "tsvector" type and Parse is only called when EF Core
                // needs a CLR value for change tracking, not in the hot path.
#pragma warning disable CS0618 // NpgsqlTsVector.Parse: acceptable in EF value-converter shim
                ValueConverter<string?, NpgsqlTsVector?> tsVectorConverter = new(
                    v => v == null ? null : NpgsqlTsVector.Parse(v),
                    v => v == null ? null : v.ToString());
#pragma warning restore CS0618
                ValueComparer<string?> tsVectorComparer = new(
                    (a, b) => a == b,
                    v => v == null ? 0 : v.GetHashCode(),
                    v => v);

                entity.Property(d => d.SearchVector)
                      .HasColumnName("search_vector")
                      .HasColumnType("tsvector")
                      .HasConversion(tsVectorConverter, tsVectorComparer)
                      .HasComputedColumnSql(
                          "setweight(to_tsvector('english', coalesce(title,'')), 'A') || " +
                          "setweight(to_tsvector('english', coalesce(content_markdown,'')), 'B')",
                          stored: true)
                      .IsRequired(false);
            }
            else
            {
                entity.Property(d => d.SearchVector).IsRequired(false);
            }

            // UNIQUE constraint on slug
            entity.HasIndex(d => d.Slug)
                  .IsUnique()
                  .HasDatabaseName("ix_doc_pages_slug");

            // NOTE: The GIN index on search_vector is created via raw SQL in the migration
            // (idx_doc_pages_search_vector) — EF cannot configure a GIN index on a
            // tsvector GENERATED column with a standard HasIndex call.
        });
    }

    private static void ConfigureRevokedJti(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RevokedJtiEntity>(entity =>
        {
            entity.ToTable("revoked_jti");

            // jti is the primary key — TEXT column
            entity.HasKey(r => r.Jti);
            entity.Property(r => r.Jti)
                  .HasColumnName("jti")
                  .IsRequired();

            // expires_at: TIMESTAMPTZ NOT NULL
            entity.Property(r => r.ExpiresAt)
                  .HasColumnName("expires_at")
                  .IsRequired();
        });
    }

    private static void ConfigureAnalysisJobs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisJobEntity>(entity =>
        {
            entity.ToTable("analysis_jobs");
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(j => j.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired();

            entity.Property(j => j.PluginVersion)
                  .HasColumnName("plugin_version")
                  .IsRequired();

            entity.Property(j => j.Status)
                  .HasColumnName("status")
                  .HasDefaultValue("queued")
                  .IsRequired();

            entity.Property(j => j.Priority)
                  .HasColumnName("priority")
                  .HasDefaultValue(0);

            entity.Property(j => j.Attempts)
                  .HasColumnName("attempts")
                  .HasDefaultValue(0);

            entity.Property(j => j.LastError)
                  .HasColumnName("last_error")
                  .IsRequired(false);

            entity.Property(j => j.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(j => j.StartedAt)
                  .HasColumnName("started_at")
                  .IsRequired(false);

            entity.Property(j => j.CompletedAt)
                  .HasColumnName("completed_at")
                  .IsRequired(false);

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(j => j.Plugin)
                  .WithMany()
                  .HasForeignKey(j => j.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(j => j.Status)
                  .HasDatabaseName("idx_analysis_jobs_status");

            entity.HasIndex(j => new { j.Priority, j.CreatedAt })
                  .IsDescending(true, false)
                  .HasDatabaseName("idx_analysis_jobs_priority");

            entity.ToTable(t => t.HasCheckConstraint(
                "chk_analysis_jobs_status",
                "status IN ('queued', 'processing', 'completed', 'failed')"));
        });
    }

    private static void ConfigureAnalysisResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisResultEntity>(entity =>
        {
            entity.ToTable("analysis_results");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(r => r.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired();

            entity.Property(r => r.PluginVersion)
                  .HasColumnName("plugin_version")
                  .IsRequired();

            entity.Property(r => r.StaticEslintScore)
                  .HasColumnName("static_eslint_score")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired(false);

            entity.Property(r => r.StaticSemgrepScore)
                  .HasColumnName("static_semgrep_score")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired(false);

            entity.Property(r => r.StaticGitleaksScore)
                  .HasColumnName("static_gitleaks_score")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired(false);

            entity.Property(r => r.StaticTrivyScore)
                  .HasColumnName("static_trivy_score")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired(false);

            entity.Property(r => r.StaticFindings)
                  .HasColumnName("static_findings")
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'[]'::jsonb")
                  .IsRequired();

            entity.Property(r => r.DynamicBehaviorScore)
                  .HasColumnName("dynamic_behavior_score")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired(false);

            entity.Property(r => r.DynamicFindings)
                  .HasColumnName("dynamic_findings")
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'[]'::jsonb")
                  .IsRequired();

            entity.Property(r => r.TotalScore)
                  .HasColumnName("total_score")
                  .HasColumnType("numeric(5,2)")
                  .IsRequired();

            entity.Property(r => r.Status)
                  .HasColumnName("status")
                  .IsRequired();

            entity.Property(r => r.AnalysisCompletedAt)
                  .HasColumnName("analysis_completed_at")
                  .IsRequired(false);

            entity.Property(r => r.StaticWeight)
                  .HasColumnName("static_weight")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(0.6m);

            entity.Property(r => r.DynamicWeight)
                  .HasColumnName("dynamic_weight")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(0.4m);

            entity.Property(r => r.PassThreshold)
                  .HasColumnName("pass_threshold")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(80m);

            entity.Property(r => r.FailThreshold)
                  .HasColumnName("fail_threshold")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(50m);

            entity.Property(r => r.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(r => r.Plugin)
                  .WithMany()
                  .HasForeignKey(r => r.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => r.PluginId)
                  .HasDatabaseName("idx_analysis_results_plugin");

            entity.HasIndex(r => r.Status)
                  .HasDatabaseName("idx_analysis_results_status");

            entity.ToTable(t => t.HasCheckConstraint(
                "chk_analysis_results_status",
                "status IN ('passed', 'failed', 'in_review')"));
        });
    }

    private static void ConfigureAnalysisConfig(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisConfigEntity>(entity =>
        {
            entity.ToTable("analysis_config");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id)
                  .HasColumnName("id")
                  .HasDefaultValue(1);

            entity.Property(c => c.StaticWeight)
                  .HasColumnName("static_weight")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(0.6m);

            entity.Property(c => c.DynamicWeight)
                  .HasColumnName("dynamic_weight")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(0.4m);

            entity.Property(c => c.PassThreshold)
                  .HasColumnName("pass_threshold")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(80m);

            entity.Property(c => c.FailThreshold)
                  .HasColumnName("fail_threshold")
                  .HasColumnType("numeric(5,2)")
                  .HasDefaultValue(50m);

            entity.Property(c => c.MaxWorkers)
                  .HasColumnName("max_workers")
                  .HasDefaultValue(2);

            entity.Property(c => c.RetryLimit)
                  .HasColumnName("retry_limit")
                  .HasDefaultValue(3);

            entity.Property(c => c.AnalysisTimeoutSeconds)
                  .HasColumnName("analysis_timeout_seconds")
                  .HasDefaultValue(300);

            entity.Property(c => c.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(c => c.UpdatedBy)
                  .HasColumnName("updated_by")
                  .IsRequired(false);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("chk_analysis_config_thresholds", "pass_threshold > fail_threshold");
                t.HasCheckConstraint("chk_analysis_config_weights", "static_weight + dynamic_weight = 1.0");
            });
        });
    }

    private static void ConfigureConfigChangeLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigChangeLogEntity>(entity =>
        {
            entity.ToTable("config_change_log");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(c => c.ChangedBy)
                  .HasColumnName("changed_by")
                  .IsRequired();

            entity.Property(c => c.PreviousConfig)
                  .HasColumnName("previous_config")
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'::jsonb")
                  .IsRequired();

            entity.Property(c => c.NewConfig)
                  .HasColumnName("new_config")
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'::jsonb")
                  .IsRequired();

            entity.Property(c => c.ChangeDescription)
                  .HasColumnName("change_description")
                  .IsRequired();

            entity.Property(c => c.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasIndex(c => c.CreatedAt)
                  .HasDatabaseName("idx_config_change_log_created_at");
        });
    }

    private static void ConfigureAppeals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppealEntity>(entity =>
        {
            entity.ToTable("appeals");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(a => a.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired();

            entity.Property(a => a.AnalysisResultId)
                  .HasColumnName("analysis_result_id")
                  .IsRequired(false);

            entity.Property(a => a.AuthorId)
                  .HasColumnName("author_id")
                  .IsRequired();

            entity.Property(a => a.Reason)
                  .HasColumnName("reason")
                  .IsRequired();

            entity.Property(a => a.Evidence)
                  .HasColumnName("evidence")
                  .IsRequired(false);

            entity.Property(a => a.Status)
                  .HasColumnName("status")
                  .HasDefaultValue("pending")
                  .IsRequired();

            entity.Property(a => a.ReviewedBy)
                  .HasColumnName("reviewed_by")
                  .IsRequired(false);

            entity.Property(a => a.ReviewedAt)
                  .HasColumnName("reviewed_at")
                  .IsRequired(false);

            entity.Property(a => a.Resolution)
                  .HasColumnName("resolution")
                  .IsRequired(false);

            entity.Property(a => a.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(a => a.Plugin)
                  .WithMany()
                  .HasForeignKey(a => a.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → analysis_results ON DELETE SET NULL
            entity.HasOne(a => a.AnalysisResult)
                  .WithMany()
                  .HasForeignKey(a => a.AnalysisResultId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);

            entity.HasIndex(a => a.PluginId)
                  .HasDatabaseName("idx_appeals_plugin");

            entity.HasIndex(a => a.AuthorId)
                  .HasDatabaseName("idx_appeals_author");

            entity.HasIndex(a => a.Status)
                  .HasDatabaseName("idx_appeals_status");

            entity.ToTable(t => t.HasCheckConstraint(
                "chk_appeals_status",
                "status IN ('pending', 'approved', 'rejected')"));
        });
    }

    private static void ConfigureSafeZonePlugins(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SafeZonePluginEntity>(entity =>
        {
            entity.ToTable("safe_zone_plugins");
            entity.HasKey(sz => sz.Id);
            entity.Property(sz => sz.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(sz => sz.OrgId)
                  .HasColumnName("org_id")
                  .IsRequired();

            entity.Property(sz => sz.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired();

            entity.Property(sz => sz.PluginVersion)
                  .HasColumnName("plugin_version")
                  .IsRequired();

            entity.Property(sz => sz.ApprovedBy)
                  .HasColumnName("approved_by")
                  .IsRequired();

            entity.Property(sz => sz.ApprovedAt)
                  .HasColumnName("approved_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(sz => sz.IsActive)
                  .HasColumnName("is_active")
                  .HasDefaultValue(true);

            // FK → organizations ON DELETE CASCADE
            entity.HasOne(sz => sz.Organization)
                  .WithMany()
                  .HasForeignKey(sz => sz.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(sz => sz.Plugin)
                  .WithMany()
                  .HasForeignKey(sz => sz.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(sz => sz.OrgId)
                  .HasDatabaseName("idx_safe_zone_org");

            entity.HasIndex(sz => sz.PluginId)
                  .HasDatabaseName("idx_safe_zone_plugin");

            // UNIQUE(org_id, plugin_id, plugin_version)
            entity.HasIndex(sz => new { sz.OrgId, sz.PluginId, sz.PluginVersion })
                  .IsUnique()
                  .HasDatabaseName("ix_safe_zone_plugins_org_plugin_version");
        });
    }

    private static void ConfigureOrgPluginBlocks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgPluginBlockEntity>(entity =>
        {
            entity.ToTable("org_plugin_blocks");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(b => b.OrgId)
                  .HasColumnName("org_id")
                  .IsRequired();

            entity.Property(b => b.PluginId)
                  .HasColumnName("plugin_id")
                  .IsRequired();

            entity.Property(b => b.BlockedBy)
                  .HasColumnName("blocked_by")
                  .IsRequired();

            entity.Property(b => b.BlockedAt)
                  .HasColumnName("blocked_at")
                  .HasDefaultValueSql("NOW()");

            // FK → organizations ON DELETE CASCADE
            entity.HasOne(b => b.Organization)
                  .WithMany()
                  .HasForeignKey(b => b.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK → plugins ON DELETE CASCADE
            entity.HasOne(b => b.Plugin)
                  .WithMany()
                  .HasForeignKey(b => b.PluginId)
                  .OnDelete(DeleteBehavior.Cascade);

            // UNIQUE(org_id, plugin_id) — one block record per org+plugin
            entity.HasIndex(b => new { b.OrgId, b.PluginId })
                  .IsUnique()
                  .HasDatabaseName("ix_org_plugin_blocks_org_plugin");
        });
    }

    private static void ConfigureBadges(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BadgeEntity>(entity =>
        {
            entity.ToTable("badges");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(b => b.Name)
                  .HasColumnName("name")
                  .IsRequired();

            entity.Property(b => b.Slug)
                  .HasColumnName("slug")
                  .IsRequired();

            entity.Property(b => b.Description)
                  .HasColumnName("description")
                  .IsRequired();

            entity.Property(b => b.IconUrl)
                  .HasColumnName("icon_url")
                  .IsRequired(false);

            entity.Property(b => b.Requirements)
                  .HasColumnName("requirements")
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'{}'::jsonb")
                  .IsRequired();

            entity.Property(b => b.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // UNIQUE on slug
            entity.HasIndex(b => b.Slug)
                  .IsUnique()
                  .HasDatabaseName("ix_badges_slug");
        });
    }

    private static void ConfigureAuthorBadges(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthorBadgeEntity>(entity =>
        {
            entity.ToTable("author_badges");
            entity.HasKey(ab => ab.Id);
            entity.Property(ab => ab.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(ab => ab.AuthorId)
                  .HasColumnName("author_id")
                  .IsRequired();

            entity.Property(ab => ab.BadgeId)
                  .HasColumnName("badge_id")
                  .IsRequired();

            entity.Property(ab => ab.AwardedAt)
                  .HasColumnName("awarded_at")
                  .HasDefaultValueSql("NOW()");

            // FK → badges ON DELETE CASCADE
            entity.HasOne(ab => ab.Badge)
                  .WithMany(b => b.AuthorBadges)
                  .HasForeignKey(ab => ab.BadgeId)
                  .OnDelete(DeleteBehavior.Cascade);

            // UNIQUE(author_id, badge_id)
            entity.HasIndex(ab => new { ab.AuthorId, ab.BadgeId })
                  .IsUnique()
                  .HasDatabaseName("ix_author_badges_author_badge");
        });
    }

    private static void ConfigureAuthorReputations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthorReputationEntity>(entity =>
        {
            entity.ToTable("author_reputation");

            // PK is the author_id — no surrogate
            entity.HasKey(ar => ar.AuthorId);
            entity.Property(ar => ar.AuthorId)
                  .HasColumnName("author_id");

            entity.Property(ar => ar.KarmaPoints)
                  .HasColumnName("karma_points")
                  .HasDefaultValue(0);

            entity.Property(ar => ar.Level)
                  .HasColumnName("level")
                  .HasDefaultValue(1);

            entity.Property(ar => ar.Badges)
                  .HasColumnName("badges")
                  .HasColumnType("jsonb")
                  .HasDefaultValueSql("'[]'::jsonb")
                  .IsRequired();

            entity.Property(ar => ar.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            entity.Property(ar => ar.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasIndex(ar => ar.KarmaPoints)
                  .HasDatabaseName("idx_author_reputation_karma");
        });
    }

    private static void ConfigureKarmaEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KarmaEventEntity>(entity =>
        {
            entity.ToTable("karma_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.AuthorId)
                  .HasColumnName("author_id")
                  .IsRequired();

            entity.Property(e => e.EventType)
                  .HasColumnName("event_type")
                  .IsRequired();

            entity.Property(e => e.Points)
                  .HasColumnName("points")
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasColumnName("description")
                  .IsRequired(false);

            entity.Property(e => e.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → author_reputation ON DELETE CASCADE
            entity.HasOne(e => e.Author)
                  .WithMany()
                  .HasForeignKey(e => e.AuthorId)
                  .HasPrincipalKey(ar => ar.AuthorId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.AuthorId);
        });
    }

    private static void ConfigureLeaderboardCache(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeaderboardCacheEntity>(entity =>
        {
            entity.ToTable("leaderboard_cache");
            entity.HasKey(lc => lc.Id);
            entity.Property(lc => lc.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(lc => lc.AuthorId)
                  .HasColumnName("author_id")
                  .IsRequired();

            entity.Property(lc => lc.KarmaPoints)
                  .HasColumnName("karma_points");

            entity.Property(lc => lc.BadgeCount)
                  .HasColumnName("badge_count")
                  .HasDefaultValue(0);

            entity.Property(lc => lc.Rank)
                  .HasColumnName("rank")
                  .IsRequired();

            entity.Property(lc => lc.Period)
                  .HasColumnName("period")
                  .HasDefaultValue("all_time")
                  .IsRequired();

            entity.Property(lc => lc.OrgId)
                  .HasColumnName("org_id")
                  .IsRequired(false);

            entity.Property(lc => lc.CalculatedAt)
                  .HasColumnName("calculated_at")
                  .HasDefaultValueSql("NOW()");

            entity.HasIndex(lc => new { lc.Period, lc.OrgId, lc.Rank })
                  .HasDatabaseName("idx_leaderboard_cache_period_org_rank");

            entity.ToTable(t => t.HasCheckConstraint(
                "chk_leaderboard_cache_period",
                "period IN ('weekly', 'monthly', 'all_time')"));
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Id)
                  .HasColumnName("id")
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(n => n.UserId)
                  .HasColumnName("user_id")
                  .IsRequired();

            entity.Property(n => n.Type)
                  .HasColumnName("type")
                  .IsRequired();

            entity.Property(n => n.Title)
                  .HasColumnName("title")
                  .IsRequired();

            entity.Property(n => n.Message)
                  .HasColumnName("message")
                  .IsRequired();

            entity.Property(n => n.IsRead)
                  .HasColumnName("is_read")
                  .HasDefaultValue(false);

            entity.Property(n => n.CreatedAt)
                  .HasColumnName("created_at")
                  .HasDefaultValueSql("NOW()");

            // FK → users ON DELETE CASCADE
            entity.HasOne(n => n.User)
                  .WithMany()
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(n => n.UserId)
                  .HasDatabaseName("idx_notifications_user");

            entity.HasIndex(n => new { n.UserId, n.IsRead })
                  .HasDatabaseName("idx_notifications_user_unread");
        });
    }

    private static void ConfigureUserNotificationPreferences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserNotificationPreferencesEntity>(entity =>
        {
            entity.ToTable("user_notification_preferences");

            // PK is the user_id — no surrogate
            entity.HasKey(p => p.UserId);
            entity.Property(p => p.UserId)
                  .HasColumnName("user_id");

            entity.Property(p => p.EmailAlerts)
                  .HasColumnName("email_alerts")
                  .HasDefaultValue(true);

            entity.Property(p => p.InAppAlerts)
                  .HasColumnName("in_app_alerts")
                  .HasDefaultValue(true);

            entity.Property(p => p.UpdatedAt)
                  .HasColumnName("updated_at")
                  .HasDefaultValueSql("NOW()");

            // FK → users ON DELETE CASCADE
            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
