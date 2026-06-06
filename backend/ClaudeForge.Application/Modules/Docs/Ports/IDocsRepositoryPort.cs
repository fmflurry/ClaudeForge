using ClaudeForge.Core.Shared.Model;

namespace ClaudeForge.Application.Modules.Docs.Ports;

/// <summary>
/// Outgoing port for documentation persistence.
/// Abstracts over the doc_pages table and plugin README surfacing.
/// </summary>
public interface IDocsRepositoryPort
{
    /// <summary>
    /// Full-text search over documentation pages.
    /// Empty/null query returns empty results gracefully (no exception).
    /// Results are ranked by relevance: title match ranks above content match.
    /// </summary>
    Task<(IReadOnlyList<DocSearchResultDto> Items, int TotalCount)> SearchAsync(
        string query,
        PaginationRequest pagination,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a documentation page by slug.
    /// Slug of form "plugin:{plugin-slug}" surfaces the plugin's latest readme_text.
    /// Returns null when the slug is not found.
    /// </summary>
    Task<DocPageDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
