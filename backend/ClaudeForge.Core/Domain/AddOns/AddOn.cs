namespace ClaudeForge.Core.Domain.AddOns;

/// <summary>
/// Immutable domain entity representing an add-on in the marketplace.
/// Pure domain — zero EF Core or infrastructure references.
/// </summary>
public sealed record AddOn
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

    public IReadOnlyList<AddOnVersion> Versions { get; init; } = [];
}
