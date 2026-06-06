using ClaudeForge.Core.Domain.Plugins;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent seeder for the 10 canonical seed plugins.
///
/// Seeding order:
///   1. Calls <see cref="ICategorySeeder.SeedAsync"/> to ensure category vocab exists.
///   2. For each <see cref="SeedPluginDefinition"/> whose name_normalized is not already present,
///      inserts the plugin, all its versions (is_latest on the last/highest only), and all
///      category associations across type / language / use_case dimensions.
///
/// Idempotency key: <c>name_normalized</c> (unique DB constraint).
/// </summary>
public sealed class PluginDataSeeder : IPluginDataSeeder
{
    private readonly MarketplaceDbContext _context;
    private readonly ICategorySeeder _categorySeeder;

    // -------------------------------------------------------------------------
    // Stable synthetic SHA-256 values (64-char hex) for each seed plugin.
    // Generated deterministically so re-seeding produces the same keys.
    // -------------------------------------------------------------------------
    private static readonly IReadOnlyList<string> SeedSha256Values =
    [
        "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
        "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3",
        "c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
        "d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5",
        "e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6",
        "f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1",
        "1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b",
        "2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c",
        "3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d",
        "4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e6f1a2b3c4d5e",
    ];

    // -------------------------------------------------------------------------
    // The 10 canonical seed plugin definitions.
    // Versions are listed ascending; the last entry is treated as latest.
    // -------------------------------------------------------------------------
    public static IReadOnlyList<SeedPluginDefinition> SeedDefinitions { get; } =
    [
        new SeedPluginDefinition(
            Name: "TypeScript Linter",
            Slug: "typescript-linter",
            Author: "ClaudeForge",
            Description: "A linter plugin for TypeScript projects that enforces code quality rules.",
            Types: ["skill"],
            Languages: ["typescript"],
            UseCases: ["dev-team"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "Python Data Analyzer",
            Slug: "python-data-analyzer",
            Author: "ClaudeForge",
            Description: "Analyzes Python data pipelines and provides statistical insights.",
            Types: ["skill", "agent"],
            Languages: ["python"],
            UseCases: ["data-analyst"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "Go Build Optimizer",
            Slug: "go-build-optimizer",
            Author: "ClaudeForge",
            Description: "Optimizes Go build configurations for faster compilation and smaller binaries.",
            Types: ["hook"],
            Languages: ["go"],
            UseCases: ["devops"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "Rust Security Scanner",
            Slug: "rust-security-scanner",
            Author: "ClaudeForge",
            Description: "Scans Rust codebases for common security vulnerabilities and unsafe patterns.",
            Types: ["skill"],
            Languages: ["rust"],
            UseCases: ["security"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "PR Review Agent",
            Slug: "pr-review-agent",
            Author: "ClaudeForge",
            Description: "Automated pull request review agent that provides actionable feedback.",
            Types: ["agent"],
            Languages: ["typescript"],
            UseCases: ["dev-team", "product-owner"],
            Versions: ["1.0.0", "1.1.0", "2.0.0"]),

        new SeedPluginDefinition(
            Name: "Deployment Commander",
            Slug: "deployment-commander",
            Author: "ClaudeForge",
            Description: "Command-line plugin for orchestrating multi-environment deployments.",
            Types: ["command"],
            Languages: ["go", "typescript"],
            UseCases: ["devops"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "Sprint Planning Assistant",
            Slug: "sprint-planning-assistant",
            Author: "ClaudeForge",
            Description: "Assists product managers with sprint planning, estimation, and backlog grooming.",
            Types: ["skill"],
            Languages: ["python"],
            UseCases: ["product-manager"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "Code Quality Plugin",
            Slug: "code-quality-plugin",
            Author: "ClaudeForge",
            Description: "Enforces code quality standards across TypeScript and Rust codebases.",
            Types: ["plugin"],
            Languages: ["typescript", "rust"],
            UseCases: ["dev-team"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "API Gateway Hook",
            Slug: "api-gateway-hook",
            Author: "ClaudeForge",
            Description: "Hook plugin for API gateway request/response transformation and security enforcement.",
            Types: ["hook"],
            Languages: ["go"],
            UseCases: ["devops", "security"],
            Versions: ["1.0.0"]),

        new SeedPluginDefinition(
            Name: "Data Pipeline Orchestrator",
            Slug: "data-pipeline-orchestrator",
            Author: "ClaudeForge",
            Description: "Orchestrates complex data pipelines across Python and Rust processing stages.",
            Types: ["agent"],
            Languages: ["python", "rust"],
            UseCases: ["data-analyst"],
            Versions: ["1.0.0"]),
    ];

    public PluginDataSeeder(MarketplaceDbContext context, ICategorySeeder categorySeeder)
    {
        _context = context;
        _categorySeeder = categorySeeder;
    }

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Step 1: ensure category vocabulary exists
        await _categorySeeder.SeedAsync(ct);

        // Step 2: load all categories into memory once for lookup
        List<CategoryEntity> allCategories = await _context.Categories
            .AsNoTracking()
            .ToListAsync(ct);

        // Step 3: insert each definition that is not already present
        for (int i = 0; i < SeedDefinitions.Count; i++)
        {
            SeedPluginDefinition def = SeedDefinitions[i];
            await SeedOnePluginAsync(def, i, allCategories, ct);
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task SeedOnePluginAsync(
        SeedPluginDefinition def,
        int definitionIndex,
        List<CategoryEntity> allCategories,
        CancellationToken ct)
    {
        string nameNormalized = def.Name.ToLowerInvariant();

        // Idempotency check: skip if already seeded
        bool exists = await _context.Plugins
            .AnyAsync(p => p.NameNormalized == nameNormalized, ct);

        if (exists)
            return;

        Guid pluginId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        PluginEntity plugin = new()
        {
            Id = pluginId,
            Name = def.Name,
            NameNormalized = nameNormalized,
            Slug = def.Slug,
            Description = def.Description,
            Author = def.Author,
            DownloadCount = 0L,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Build versions — last entry is latest
        for (int versionIndex = 0; versionIndex < def.Versions.Count; versionIndex++)
        {
            string versionString = def.Versions[versionIndex];
            bool isLatest = versionIndex == def.Versions.Count - 1;
            long versionSort = SemVer.Parse(versionString).ToVersionSort();

            // Use a stable, per-plugin sha256 for all versions of the same plugin
            string sha256 = SeedSha256Values[definitionIndex % SeedSha256Values.Count];

            PluginVersionEntity version = new()
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                Version = versionString,
                VersionSort = versionSort,
                ReleaseNotes = string.Empty,
                IsLatest = isLatest,
                PackageKey = $"plugins/{pluginId}/{versionString}/package.tar.gz",
                PackageFormat = "tar.gz",
                SizeBytes = 4096L + (versionIndex * 512L),
                Sha256 = sha256,
                DownloadCount = 0L,
                ReadmeText = null,
                ReleasedAt = now,
            };

            plugin.Versions.Add(version);
        }

        _context.Plugins.Add(plugin);

        // Save plugin + versions first so the plugin ID is valid for FK references
        await _context.SaveChangesAsync(ct);

        // Build category associations
        List<PluginCategoryEntity> categoryAssociations = BuildCategoryAssociations(
            pluginId, def, allCategories);

        _context.PluginCategories.AddRange(categoryAssociations);
        await _context.SaveChangesAsync(ct);
    }

    private static List<PluginCategoryEntity> BuildCategoryAssociations(
        Guid pluginId,
        SeedPluginDefinition def,
        List<CategoryEntity> allCategories)
    {
        List<PluginCategoryEntity> associations = [];

        foreach (string typeValue in def.Types)
        {
            CategoryEntity? category = allCategories
                .FirstOrDefault(c => c.Dimension == "type" && c.Value == typeValue);

            if (category is not null)
            {
                associations.Add(new PluginCategoryEntity
                {
                    PluginId = pluginId,
                    CategoryId = category.Id,
                });
            }
        }

        foreach (string languageValue in def.Languages)
        {
            CategoryEntity? category = allCategories
                .FirstOrDefault(c => c.Dimension == "language" && c.Value == languageValue);

            if (category is not null)
            {
                associations.Add(new PluginCategoryEntity
                {
                    PluginId = pluginId,
                    CategoryId = category.Id,
                });
            }
        }

        foreach (string useCaseValue in def.UseCases)
        {
            CategoryEntity? category = allCategories
                .FirstOrDefault(c => c.Dimension == "use_case" && c.Value == useCaseValue);

            if (category is not null)
            {
                associations.Add(new PluginCategoryEntity
                {
                    PluginId = pluginId,
                    CategoryId = category.Id,
                });
            }
        }

        return associations;
    }
}
