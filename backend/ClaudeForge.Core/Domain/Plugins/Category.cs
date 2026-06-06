namespace ClaudeForge.Core.Domain.Plugins;

/// <summary>
/// Immutable domain entity representing a controlled-vocabulary category entry.
/// Categories are organized by dimension: type, language, use_case.
/// Pure domain — zero EF Core or infrastructure references.
/// </summary>
public sealed record Category
{
    public required short Id { get; init; }
    public required string Dimension { get; init; }
    public required string Value { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
}
