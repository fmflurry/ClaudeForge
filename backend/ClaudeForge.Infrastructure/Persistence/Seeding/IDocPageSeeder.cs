namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Contract for seeding the 5 canonical documentation pages into the marketplace database.
/// Implementations must be idempotent (safe to call multiple times).
/// </summary>
public interface IDocPageSeeder
{
    /// <summary>
    /// Seeds all 5 canonical documentation pages.
    /// Existing pages (keyed by slug) are skipped.
    /// </summary>
    Task SeedAsync(CancellationToken ct = default);
}
