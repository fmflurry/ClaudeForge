using ClaudeForge.Application.Modules.Docs.Ports;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Docs;

/// <summary>
/// EF Core adapter for IDocsRepositoryPort.
/// Implements full-text search over doc_pages and plugin README surfacing.
///
/// Search: ts_rank over doc_pages.search_vector (title weighted A, content_markdown weighted B).
/// GetBySlug: "plugin:{slug}" → surfaces latest plugin readme_text; otherwise → doc_pages lookup.
/// </summary>
public sealed class DocsRepositoryAdapter : IDocsRepositoryPort
{
    private const string PluginSlugPrefix = "plugin:";
    private const string NoReadmePlaceholder = "No detailed documentation provided";

    private readonly MarketplaceDbContext _context;

    public DocsRepositoryAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<DocSearchResultDto> Items, int TotalCount)> SearchAsync(
        string query,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        string trimmedQuery = query?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return ([], 0);
        }

        // Use parameterized raw SQL for full-text search with ts_rank.
        // search_vector is a GENERATED STORED column weighted: title=A, content_markdown=B.
        // ts_rank returns a float; we normalize it to [0,1] after fetching.
        int skip = (pagination.Page - 1) * pagination.Limit;
        int take = pagination.Limit;

        // Count matching rows
        List<int> countResult = await _context.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int
                FROM doc_pages
                WHERE search_vector @@ plainto_tsquery('english', @query)
                """,
                new Npgsql.NpgsqlParameter("@query", trimmedQuery))
            .ToListAsync(ct);

        int totalCount = countResult.FirstOrDefault();

        if (totalCount == 0)
        {
            return ([], 0);
        }

        // Fetch ranked rows
        List<DocSearchRow> rows = await _context.Database
            .SqlQueryRaw<DocSearchRow>(
                """
                SELECT
                    slug AS "Slug",
                    title AS "Title",
                    category AS "Category",
                    content_markdown AS "ContentMarkdown",
                    ts_rank(search_vector, plainto_tsquery('english', @query)) AS "RawScore"
                FROM doc_pages
                WHERE search_vector @@ plainto_tsquery('english', @query)
                ORDER BY "RawScore" DESC
                LIMIT @take OFFSET @skip
                """,
                new Npgsql.NpgsqlParameter("@query", trimmedQuery),
                new Npgsql.NpgsqlParameter("@take", take),
                new Npgsql.NpgsqlParameter("@skip", skip))
            .ToListAsync(ct);

        float maxScore = rows.Count > 0 ? rows.Max(r => r.RawScore) : 1f;
        if (maxScore <= 0f) maxScore = 1f;

        IReadOnlyList<DocSearchResultDto> items = rows
            .Select(r => MapToSearchResultDto(r, maxScore))
            .ToList();

        return (items, totalCount);
    }

    public async Task<DocPageDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (slug.StartsWith(PluginSlugPrefix, StringComparison.Ordinal))
        {
            return await GetPluginDocBySlugAsync(slug, ct);
        }

        return await GetStaticDocBySlugAsync(slug, ct);
    }

    // -------------------------------------------------------------------------
    // Plugin README surfacing
    // Spec: "Documentation synced from plugin metadata"
    //       slug convention: "plugin:{plugin-slug}"
    //       placeholder when readme_text is null: "No detailed documentation provided"
    // -------------------------------------------------------------------------

    private async Task<DocPageDto?> GetPluginDocBySlugAsync(string slug, CancellationToken ct)
    {
        string pluginSlug = slug.Substring(PluginSlugPrefix.Length);

        AddOnEntity? plugin = await _context.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == pluginSlug, ct);

        if (plugin is null)
        {
            return null;
        }

        AddOnVersionEntity? latestVersion = await _context.PluginVersions
            .AsNoTracking()
            .Where(v => v.PluginId == plugin.Id && v.IsLatest)
            .FirstOrDefaultAsync(ct);

        string contentMarkdown = latestVersion?.ReadmeText is { Length: > 0 } readmeText
            ? readmeText
            : NoReadmePlaceholder;

        // Extract title from the first H1 heading in the readme, fallback to plugin name.
        string title = ExtractReadmeTitle(contentMarkdown) ?? plugin.Name;

        return new DocPageDto
        {
            Slug = slug,
            Title = title,
            Category = "Plugins",
            ContentMarkdown = contentMarkdown,
            LastUpdated = latestVersion?.ReleasedAt ?? plugin.UpdatedAt,
        };
    }

    // -------------------------------------------------------------------------
    // Static doc page lookup from doc_pages table
    // -------------------------------------------------------------------------

    private async Task<DocPageDto?> GetStaticDocBySlugAsync(string slug, CancellationToken ct)
    {
        DocPageEntity? entity = await _context.DocPages
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Slug == slug, ct);

        if (entity is null)
        {
            return null;
        }

        return new DocPageDto
        {
            Slug = entity.Slug,
            Title = entity.Title,
            Category = entity.Category,
            ContentMarkdown = entity.ContentMarkdown,
            LastUpdated = entity.LastUpdated,
        };
    }

    // -------------------------------------------------------------------------
    // Mapping
    // -------------------------------------------------------------------------

    private static DocSearchResultDto MapToSearchResultDto(DocSearchRow row, float maxScore)
    {
        float normalized = maxScore > 0 ? Math.Clamp(row.RawScore / maxScore, 0f, 1f) : 0f;

        string snippet = BuildSnippet(row.ContentMarkdown);

        return new DocSearchResultDto
        {
            Slug = row.Slug,
            Title = row.Title,
            Category = row.Category,
            Snippet = snippet,
            RelevanceScore = normalized,
        };
    }

    /// <summary>
    /// Extracts the title from the first markdown H1 heading (# Title).
    /// Returns null when no H1 heading is found.
    /// </summary>
    private static string? ExtractReadmeTitle(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                string title = trimmed.Substring(2).Trim();
                if (title.Length > 0)
                {
                    return title;
                }
            }
        }

        return null;
    }

    private static string BuildSnippet(string content)
    {
        const int maxLength = 200;

        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        // Strip leading markdown headings for a cleaner snippet
        string text = content.TrimStart('#', ' ', '\n', '\r');
        if (text.Length <= maxLength)
        {
            return text.Trim();
        }

        int cutoff = text.LastIndexOf(' ', maxLength);
        if (cutoff <= 0) cutoff = maxLength;

        return text.Substring(0, cutoff).Trim() + "...";
    }
}

/// <summary>
/// Internal projection class for raw SQL doc_pages search results.
/// </summary>
internal sealed class DocSearchRow
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public float RawScore { get; set; }
}
