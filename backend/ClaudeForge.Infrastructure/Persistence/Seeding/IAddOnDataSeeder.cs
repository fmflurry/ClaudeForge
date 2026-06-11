namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Contract for seeding the 10 canonical test/demo plugins into the marketplace database.
/// Implementations must be idempotent (safe to call multiple times).
/// </summary>
public interface IAddOnDataSeeder
{
    /// <summary>
    /// Seeds all 10 canonical plugin definitions plus their versions and category associations.
    /// Calls <see cref="ICategorySeeder.SeedAsync"/> internally to ensure category vocab exists.
    /// Existing plugins (keyed by name_normalized) are skipped.
    /// </summary>
    Task SeedAsync(CancellationToken ct = default);
}
