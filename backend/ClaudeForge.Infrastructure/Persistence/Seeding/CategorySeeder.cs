using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent seeder for the controlled-vocabulary <c>categories</c> table.
/// Seeds 15 rows across three dimensions:
/// <list type="bullet">
///   <item>type — skill, hook, agent, command, plugin (5)</item>
///   <item>language — typescript, python, go, rust (4)</item>
///   <item>use_case — dev-team, product-owner, product-manager, devops, security, data-analyst (6)</item>
/// </list>
/// Safe to call multiple times; existing rows are skipped.
/// </summary>
public sealed class CategorySeeder : ICategorySeeder
{
    private readonly MarketplaceDbContext _context;

    public CategorySeeder(MarketplaceDbContext context)
    {
        _context = context;
    }

    private static readonly IReadOnlyList<(string Dimension, string Value, string DisplayName)> VocabularyRows =
    [
        // type dimension (5)
        ("type", "skill",   "Skill"),
        ("type", "hook",    "Hook"),
        ("type", "agent",   "Agent"),
        ("type", "command", "Command"),
        ("type", "plugin",  "Plugin"),

        // language dimension (4)
        ("language", "typescript", "TypeScript"),
        ("language", "python",     "Python"),
        ("language", "go",         "Go"),
        ("language", "rust",       "Rust"),

        // use_case dimension (6)
        ("use_case", "dev-team",          "Developer Team"),
        ("use_case", "product-owner",     "Product Owner"),
        ("use_case", "product-manager",   "Product Manager"),
        ("use_case", "devops",            "DevOps"),
        ("use_case", "security",          "Security"),
        ("use_case", "data-analyst",      "Data Analyst"),
    ];

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach ((string dimension, string value, string displayName) in VocabularyRows)
        {
            bool exists = await _context.Categories
                .AnyAsync(c => c.Dimension == dimension && c.Value == value, cancellationToken);

            if (!exists)
            {
                _context.Categories.Add(new CategoryEntity
                {
                    Dimension = dimension,
                    Value = value,
                    DisplayName = displayName,
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
