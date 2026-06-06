using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NpgsqlTypes;

namespace ClaudeForge.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the plugin marketplace.
/// Owns all six tables: plugins, plugin_versions, categories,
/// plugin_categories, telemetry_events, and telemetry_aggregates.
/// </summary>
public sealed class MarketplaceDbContext : DbContext
{
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    public DbSet<PluginEntity> Plugins => Set<PluginEntity>();
    public DbSet<PluginVersionEntity> PluginVersions => Set<PluginVersionEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<PluginCategoryEntity> PluginCategories => Set<PluginCategoryEntity>();
    public DbSet<TelemetryEventEntity> TelemetryEvents => Set<TelemetryEventEntity>();
    public DbSet<TelemetryAggregateEntity> TelemetryAggregates => Set<TelemetryAggregateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePlugins(modelBuilder);
        ConfigurePluginVersions(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigurePluginCategories(modelBuilder);
        ConfigureTelemetryEvents(modelBuilder);
        ConfigureTelemetryAggregates(modelBuilder);
    }

    private static void ConfigurePlugins(ModelBuilder modelBuilder)
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

            // search_vector is a PostgreSQL tsvector STORED GENERATED column.
            // We use a ValueConverter to bridge the CLR string? ↔ NpgsqlTsVector mapping.
            // PostgreSQL evaluates to_tsvector('english', name || ' ' || description) on every write.
            // EF reads the tsvector back and the converter converts NpgsqlTsVector.ToString() to string?.
            ValueConverter<string?, NpgsqlTsVector?> tsVectorConverter = new(
                v => v == null ? null : NpgsqlTsVector.Parse(v),
                v => v == null ? null : v.ToString());

            entity.Property(p => p.SearchVector)
                  .HasColumnName("search_vector")
                  .HasColumnType("tsvector")
                  .HasConversion(tsVectorConverter)
                  .HasComputedColumnSql(
                      "to_tsvector('english', coalesce(name,'') || ' ' || coalesce(description,''))",
                      stored: true)
                  .IsRequired(false);

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
            // tsvector column that is mapped as string.
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
}
