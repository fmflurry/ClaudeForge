using ClaudeForge.Application.Modules.AddOnSearch.Ports;
using ClaudeForge.Application.Modules.AddOnSearch.UseCases;
using ClaudeForge.Core.Shared.Model;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.AddOnSearch;

/// <summary>
/// PostgreSQL full-text search adapter implementing ISearchIndexPort.
/// Uses tsvector + ts_rank for relevance, blended with download_count and recency as tiebreakers.
/// Category filters: OR within a dimension, AND across dimensions.
/// RelevanceScore is normalized to [0, 1].
/// MaturityIndicator: "new" (created within 90 days), "deprecated" (flagged by name), "stable" otherwise.
/// Visibility filter: visibility='public' OR owner_org_id = ANY(@viewerOrgIds).
/// </summary>
public sealed class PostgresSearchAdapter : ISearchIndexPort
{
    private readonly MarketplaceDbContext _context;

    public PostgresSearchAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        CancellationToken ct = default)
        => SearchAsync(criteria, pagination, new HashSet<Guid>(), ct);

    public async Task<(IReadOnlyList<SearchResultDto> Items, int TotalCount)> SearchAsync(
        SearchCriteria criteria,
        PaginationRequest pagination,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        string? query = criteria.Query?.Trim();
        bool hasQuery = !string.IsNullOrEmpty(query);

        (List<SearchRow> rows, int total) = await ExecuteSearchSqlAsync(
            criteria, pagination, hasQuery, viewerOrgIds, ct);

        float maxScore = rows.Count > 0 ? rows.Max(r => r.RawScore) : 1f;
        if (maxScore <= 0f) maxScore = 1f;

        IReadOnlyList<SearchResultDto> items = rows
            .Select(r => MapToSearchDto(r, maxScore))
            .ToList();

        return (items, total);
    }

    /// <summary>Backward-compatible overload without viewerOrgIds (public-only).</summary>
    public Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        CancellationToken ct = default)
        => DiscoverAsync(criteria, new HashSet<Guid>(), ct);

    public async Task<(IReadOnlyList<DiscoveryResultDto> Items, int TotalCount)> DiscoverAsync(
        SearchCriteria criteria,
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct = default)
    {
        string? query = criteria.Query?.Trim();
        bool hasQuery = !string.IsNullOrEmpty(query);

        (List<SearchRow> rows, int total) = await ExecuteSearchSqlAsync(
            criteria, PaginationRequest.Default, hasQuery, viewerOrgIds, ct);

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
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct)
    {
        int skip = (pagination.Page - 1) * pagination.Limit;
        int take = pagination.Limit;
        string? queryTerm = hasQuery ? criteria.Query!.Trim() : null;

        int total = await CountSearchAsync(criteria, queryTerm, viewerOrgIds, ct);
        List<SearchRow> rows = await FetchSearchRowsAsync(criteria, queryTerm, skip, take, viewerOrgIds, ct);

        return (rows, total);
    }

    private async Task<int> CountSearchAsync(
        SearchCriteria criteria,
        string? query,
        IReadOnlySet<Guid> viewerOrgIds,
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

        (string visibilityWhere, Npgsql.NpgsqlParameter? visParam) = BuildVisibilityClause(viewerOrgIds);

        string countSql = $"""
            SELECT COUNT(DISTINCT p.id)::int
            FROM plugins p
            {typeJoin}
            {langJoin}
            {ucJoin}
            WHERE 1=1
            {visibilityWhere}
            {countFtsWhere}
            """;

        List<Npgsql.NpgsqlParameter> countParams = [.. filterParams];
        if (visParam is not null)
            countParams.Add(visParam);
        if (query != null)
            countParams.Add(new Npgsql.NpgsqlParameter("@query", query));

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
        IReadOnlySet<Guid> viewerOrgIds,
        CancellationToken ct)
    {
        (string typeJoin, string langJoin, string ucJoin, List<Npgsql.NpgsqlParameter> filterParams) =
            BuildFilterJoinClauses(criteria);

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

        (string visibilityWhere, Npgsql.NpgsqlParameter? visParam) = BuildVisibilityClause(viewerOrgIds);

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
            {visibilityWhere}
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

        if (visParam is not null)
            parameters.Add(visParam);

        if (query != null)
            parameters.Add(new Npgsql.NpgsqlParameter("@query", query));

        return await _context.Database
            .SqlQueryRaw<SearchRow>(dataSql, [.. parameters])
            .ToListAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Visibility clause builder
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns an AND WHERE clause for the visibility predicate plus any parameter needed.
    /// When viewerOrgIds is empty: AND p.visibility = 'public'
    /// When viewerOrgIds is non-empty: AND (p.visibility = 'public' OR p.owner_org_id = ANY(@viewerOrgIds))
    /// </summary>
    private static (string Clause, Npgsql.NpgsqlParameter? Param) BuildVisibilityClause(
        IReadOnlySet<Guid> viewerOrgIds)
    {
        if (viewerOrgIds.Count == 0)
        {
            return ("AND p.visibility = 'public'", null);
        }

        Guid[] idsArray = viewerOrgIds.ToArray();
        Npgsql.NpgsqlParameter param = new Npgsql.NpgsqlParameter("@viewerOrgIds", idsArray)
        {
            NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid,
        };

        return (
            "AND (p.visibility = 'public' OR p.owner_org_id = ANY(@viewerOrgIds))",
            param);
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
