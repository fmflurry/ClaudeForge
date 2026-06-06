namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Contract for seeding the controlled-vocabulary category table.
/// Implementations must be idempotent (safe to call multiple times).
/// </summary>
public interface ICategorySeeder
{
    /// <summary>
    /// Seeds all required category vocabulary rows.
    /// Existing rows are left unchanged; only missing rows are inserted.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
