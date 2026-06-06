namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Immutable descriptor for a single seed documentation page.
/// </summary>
public sealed record DocPageSeedDefinition(
    string Slug,
    string Title,
    string Category,
    string ContentMarkdown);
