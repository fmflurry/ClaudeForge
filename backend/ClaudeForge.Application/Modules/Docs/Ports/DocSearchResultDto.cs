namespace ClaudeForge.Application.Modules.Docs.Ports;

/// <summary>
/// DTO for a single documentation search result.
/// Spec: { slug, title, category, snippet, relevanceScore }
/// </summary>
public sealed class DocSearchResultDto
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Snippet { get; init; }
    public required float RelevanceScore { get; init; }
}
