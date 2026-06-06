namespace ClaudeForge.Application.Modules.Docs.Ports;

/// <summary>
/// DTO for a full documentation page.
/// Design §7: { slug, title, category, content (markdown), last_updated }
/// </summary>
public sealed class DocPageDto
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string ContentMarkdown { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
}
