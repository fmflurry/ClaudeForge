namespace ClaudeForge.Core.Domain.Plugins;

/// <summary>
/// Immutable domain entity representing a plugin in the marketplace.
/// Pure domain — zero EF Core or infrastructure references.
/// </summary>
public sealed record Plugin
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string NameNormalized { get; init; }
    public required string Slug { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
    public long DownloadCount { get; init; }
    public string? SearchVector { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public IReadOnlyList<PluginVersion> Versions { get; init; } = [];
}
