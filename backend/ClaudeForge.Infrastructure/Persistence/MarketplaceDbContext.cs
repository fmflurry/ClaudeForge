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
                ValueConverter<string?, NpgsqlTsVector?> tsVectorConverter = new(
                    v => v == null ? null : NpgsqlTsVector.Parse(v),
                    v => v == null ? null : v.ToString());
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
            entity.ToTable(t => t.HasCheckConstraint(
                "chk_visibility_owner",
                "visibility = 'public' OR owner_org_id IS NOT NULL"));

            // Composite index for visibility + owner_org_id filter queries
            entity.HasIndex(p => new { p.Visibility, p.OwnerOrgId })
                  .HasDatabaseName("idx_plugins_visibility_org");
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
                ValueConverter<string?, NpgsqlTsVector?> tsVectorConverter = new(
                    v => v == null ? null : NpgsqlTsVector.Parse(v),
                    v => v == null ? null : v.ToString());
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
}
