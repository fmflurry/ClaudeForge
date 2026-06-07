using ClaudeForge.Application.Modules.PluginSearch.Ports;
using ClaudeForge.Application.Modules.PluginSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.PluginSearch;

/// <summary>
/// PostgreSQL full-text search adapter implementing ISearchIndexPort.
/// Uses tsvector + ts_rank for relevance, blended with download_count and recency as tiebreakers.
/// Category filters: OR within a dimension, AND across dimensions.
/// RelevanceScore is normalized to [0, 1].
/// MaturityIndicator: "new" (created within 90 days), "deprecated" (flagged by name), "stable" otherwise.
/// </summary>
public sealed class PostgresSearchAdapter : ISearchIndexPort
{
    private readonly MarketplaceDbContext _context;

    public PostgresSearchAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        CancellationToken ct = default)
    {
        string? query = criteria.Query?.Trim();
        bool hasQuery = !string.IsNullOrEmpty(query);

        // Build base SQL with FTS ranking
        // We use plainto_tsquery for robust tokenisation (handles case, stop words, stemming).
        // Ranking formula: ts_rank(search_vector, tsquery) * 2 + log(1 + download_count) / 10 + epoch_age_score
        // This ensures: FTS score is primary, downloads are tiebreaker, recency is secondary tiebreaker.
        // Execute via EF raw SQL
        (List<SearchRow> rows, int total) = await ExecuteSearchSqlAsync(
            criteria, pagination, hasQuery, ct);

        float maxScore = rows.Count > 0 ? rows.Max(r => r.RawScore) : 1f;
        if (maxScore <= 0f) maxScore = 1f;

        IReadOnlyList<SearchResultDto> items = rows
            .Select(r => MapToSearchDto(r, maxScore))
            .ToList();

        return (items, total);
    }

    public async Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        CancellationToken ct = default)
    {
        string? query = criteria.Query?.Trim();
        bool hasQuery = !string.IsNullOrEmpty(query);

        (List<SearchRow> rows, int total) = await ExecuteSearchSqlAsync(
            criteria, PaginationRequest.Default, hasQuery, ct);

        float maxScore = rows.Count > 0 ? rows.Max(r => r.RawScore) : 1f;
        if (maxScore <= 0f) maxScore = 1f;

        IReadOnlyList<DiscoveryResultDto> items = rows
            .Select(r => MapToDiscoveryDto(r, maxScore))
            .ToList();

        return (items, total);
    }

    // -------------------------------------------------------------------------
    // Core SQL execution
    // -------------------------------------------------------------------------

    private async Task<(List<SearchRow> Rows, int Total)> ExecuteSearchSqlAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        bool hasQuery,
        CancellationToken ct)
    {
        int skip = (pagination.Page - 1) * pagination.Limit;
        int take = pagination.Limit;
        string? queryTerm = hasQuery ? criteria.Query!.Trim() : null;

        int total = await CountSearchAsync(criteria, queryTerm, ct);
        List<SearchRow> rows = await FetchSearchRowsAsync(criteria, queryTerm, skip, take, ct);

        return (rows, total);
    }

    private async Task<int> CountSearchAsync(
        SearchCriteria criteria,
        string? query,
        CancellationToken ct)
    {
        // Build category filter CTE
        (string typeJoin, string langJoin, string ucJoin, List<Npgsql.NpgsqlParameter> filterParams) =
            BuildFilterJoinClauses(criteria);

        string countFtsWhere = query != null
            ? """
              AND (
                  (p.search_vector IS NOT NULL AND p.search_vector @@ plainto_tsquery('english', @query))
                  OR p.name ILIKE '%' || @query || '%'
                  OR p.description ILIKE '%' || @query || '%'
              )
              """
            : string.Empty;

        string countSql = $"""
            SELECT COUNT(DISTINCT p.id)::int
            FROM plugins p
            {typeJoin}
            {langJoin}
            {ucJoin}
            WHERE 1=1
            {countFtsWhere}
            """;

        List<Npgsql.NpgsqlParameter> countParams = [.. filterParams];
        if (query != null)
        {
            countParams.Add(new Npgsql.NpgsqlParameter("@query", query));
        }

        List<int> countResult = await _context.Database
            .SqlQueryRaw<int>(countSql, [.. countParams])
            .ToListAsync(ct);

        return countResult.FirstOrDefault();
    }

    private async Task<List<SearchRow>> FetchSearchRowsAsync(
        SearchCriteria criteria,
        string? query,
        int skip,
        int take,
        CancellationToken ct)
    {
        (string typeJoin, string langJoin, string ucJoin, List<Npgsql.NpgsqlParameter> filterParams) =
            BuildFilterJoinClauses(criteria);

        // Ranking: FTS rank (primary) + download popularity (secondary) + recency (tertiary)
        // ts_rank returns 0..1 range, so multiply by 2 to ensure FTS dominates.
        // log10(1 + download_count) normalises download popularity.
        // epoch / 1e11 gives a small recency bonus (newer = higher epoch).
        // Ranking: hybrid FTS (primary) + download popularity (secondary) + recency (tertiary).
        // ts_rank is wrapped in COALESCE to handle NULL search_vector gracefully.
        // The ILIKE fallback ensures prefix/substring queries also score (e.g. 'auth' → AuthHelper).
        string rankExpr = query != null
            ? """
              (CASE WHEN p.search_vector IS NOT NULL AND p.search_vector @@ plainto_tsquery('english', @query)
                    THEN ts_rank(p.search_vector, plainto_tsquery('english', @query)) * 2.0
                    ELSE 0.0 END
              + CASE WHEN p.name ILIKE '%' || @query || '%' THEN 1.5
                     WHEN p.description ILIKE '%' || @query || '%' THEN 0.5
                     ELSE 0.0 END)
              + log(1 + p.download_count) / 20.0
              + extract(epoch from p.created_at) / 1e11
              """
            : "log(1 + p.download_count) / 20.0 + extract(epoch from p.created_at) / 1e11";

        string ftsWhere = query != null
            ? """
              AND (
                  (p.search_vector IS NOT NULL AND p.search_vector @@ plainto_tsquery('english', @query))
                  OR p.name ILIKE '%' || @query || '%'
                  OR p.description ILIKE '%' || @query || '%'
              )
              """
            : string.Empty;

        string dataSql = $"""
            SELECT
                p.id AS "Id",
                p.name AS "Name",
                p.slug AS "Slug",
                p.description AS "Description",
                p.author AS "Author",
                p.download_count AS "DownloadCount",
                p.created_at AS "CreatedAt",
                p.updated_at AS "UpdatedAt",
                ({rankExpr}) AS "RawScore",
                v.version AS "LatestVersion",
                COALESCE(
                    (SELECT string_agg(c.value, ',' ORDER BY c.value)
                     FROM plugin_categories pc2
                     JOIN categories c ON c.id = pc2.category_id
                     WHERE pc2.plugin_id = p.id AND c.dimension = 'type'), '') AS "TypeValues",
                COALESCE(
                    (SELECT string_agg(c.value, ',' ORDER BY c.value)
                     FROM plugin_categories pc3
                     JOIN categories c ON c.id = pc3.category_id
                     WHERE pc3.plugin_id = p.id AND c.dimension = 'language'), '') AS "LanguageValues",
                COALESCE(
                    (SELECT string_agg(c.value, ',' ORDER BY c.value)
                     FROM plugin_categories pc4
                     JOIN categories c ON c.id = pc4.category_id
                     WHERE pc4.plugin_id = p.id AND c.dimension = 'use_case'), '') AS "UseCaseValues"
            FROM plugins p
            {typeJoin}
            {langJoin}
            {ucJoin}
            LEFT JOIN plugin_versions v ON v.plugin_id = p.id AND v.is_latest = TRUE
            WHERE 1=1
            {ftsWhere}
            GROUP BY p.id, p.name, p.slug, p.description, p.author, p.download_count,
                     p.created_at, p.updated_at, v.version
            ORDER BY "RawScore" DESC, p.download_count DESC, p.created_at DESC
            LIMIT @take OFFSET @skip
            """;

        List<Npgsql.NpgsqlParameter> parameters =
        [
            new Npgsql.NpgsqlParameter("@take", take),
            new Npgsql.NpgsqlParameter("@skip", skip),
            .. filterParams,
        ];

        if (query != null)
        {
            parameters.Add(new Npgsql.NpgsqlParameter("@query", query));
        }

        return await _context.Database
            .SqlQueryRaw<SearchRow>(dataSql, [.. parameters])
            .ToListAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Filter clause builder
    // -------------------------------------------------------------------------

    // Returns parameterized JOIN clauses using = ANY(@param) to prevent SQL injection.
    // Each active filter dimension adds one NpgsqlParameter with a string[] value.
    private static (string TypeJoin, string LangJoin, string UcJoin,
                    List<Npgsql.NpgsqlParameter> FilterParams) BuildFilterJoinClauses(
        SearchCriteria criteria)
    {
        string typeJoin = string.Empty;
        string langJoin = string.Empty;
        string ucJoin = string.Empty;
        List<Npgsql.NpgsqlParameter> filterParams = [];

        if (criteria.TypeFilter is { Count: > 0 })
        {
            typeJoin = """
                JOIN plugin_categories pc_type ON pc_type.plugin_id = p.id
                JOIN categories c_type ON c_type.id = pc_type.category_id
                    AND c_type.dimension = 'type'
                    AND c_type.value = ANY(@typeFilter)
                """;
            filterParams.Add(new Npgsql.NpgsqlParameter("@typeFilter", criteria.TypeFilter.ToArray()));
        }

        if (criteria.LanguageFilter is { Count: > 0 })
        {
            langJoin = """
                JOIN plugin_categories pc_lang ON pc_lang.plugin_id = p.id
                JOIN categories c_lang ON c_lang.id = pc_lang.category_id
                    AND c_lang.dimension = 'language'
                    AND c_lang.value = ANY(@langFilter)
                """;
            filterParams.Add(new Npgsql.NpgsqlParameter("@langFilter", criteria.LanguageFilter.ToArray()));
        }

        if (criteria.UseCaseFilter is { Count: > 0 })
        {
            ucJoin = """
                JOIN plugin_categories pc_uc ON pc_uc.plugin_id = p.id
                JOIN categories c_uc ON c_uc.id = pc_uc.category_id
                    AND c_uc.dimension = 'use_case'
                    AND c_uc.value = ANY(@ucFilter)
                """;
            filterParams.Add(new Npgsql.NpgsqlParameter("@ucFilter", criteria.UseCaseFilter.ToArray()));
        }

        return (typeJoin, langJoin, ucJoin, filterParams);
    }

    // -------------------------------------------------------------------------
    // Mapping
    // -------------------------------------------------------------------------

    private static SearchResultDto MapToSearchDto(SearchRow row, float maxScore)
    {
        float normalised = maxScore > 0 ? Math.Clamp(row.RawScore / maxScore, 0f, 1f) : 0f;

        return new SearchResultDto
        {
            Id = row.Id,
            Name = row.Name,
            Slug = row.Slug,
            Description = row.Description,
            RelevanceScore = normalised,
            DownloadCount = row.DownloadCount,
            LatestVersion = string.IsNullOrEmpty(row.LatestVersion) ? null : row.LatestVersion,
            CreatedAt = row.CreatedAt,
            Types = SplitCategories(row.TypeValues),
            Languages = SplitCategories(row.LanguageValues),
            UseCases = SplitCategories(row.UseCaseValues),
        };
    }

    private static DiscoveryResultDto MapToDiscoveryDto(SearchRow row, float maxScore)
    {
        float normalised = maxScore > 0 ? Math.Clamp(row.RawScore / maxScore, 0f, 1f) : 0f;

        return new DiscoveryResultDto
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            LatestVersion = string.IsNullOrEmpty(row.LatestVersion) ? null : row.LatestVersion,
            Types = SplitCategories(row.TypeValues),
            Languages = SplitCategories(row.LanguageValues),
            UseCases = SplitCategories(row.UseCaseValues),
            RelevanceScore = normalised,
            DownloadCount = row.DownloadCount,
            LastUpdated = row.UpdatedAt,
            Author = row.Author,
            MaturityIndicator = DetermineMaturityIndicator(row),
        };
    }

    private static string DetermineMaturityIndicator(SearchRow row)
    {
        // "deprecated" if the plugin name or description contains the word "deprecated"
        if (row.Name.Contains("deprecated", StringComparison.OrdinalIgnoreCase) ||
            row.Description.Contains("deprecated", StringComparison.OrdinalIgnoreCase))
        {
            return "deprecated";
        }

        // "new" if created within the last 90 days
        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        if (row.CreatedAt > cutoff)
        {
            return "new";
        }

        return "stable";
    }

    private static IReadOnlyList<string> SplitCategories(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

}
