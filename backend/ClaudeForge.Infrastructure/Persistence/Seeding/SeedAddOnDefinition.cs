namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Immutable descriptor for a single seed plugin.
/// Versions must be listed in ascending semver order; the last entry is the latest.
/// </summary>
public sealed record SeedAddOnDefinition(
    string Name,
    string Slug,
    string Author,
    string Description,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> UseCases,
    IReadOnlyList<string> Versions);
