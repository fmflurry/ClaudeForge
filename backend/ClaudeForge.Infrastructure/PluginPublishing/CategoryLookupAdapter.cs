using ClaudeForge.Application.Modules.PluginPublishing.Ports;
using ClaudeForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.PluginPublishing;

/// <summary>
/// EF Core adapter implementing <see cref="ICategoryLookupPort"/> for use during plugin
/// publishing. Loads all categories once and returns them as a key → id dictionary so the
/// publishing use case can validate manifest tags without issuing per-value queries.
/// Key format: <c>"dimension:value"</c> (e.g. <c>"type:skill"</c>, <c>"use_case:devops"</c>).
/// </summary>
public sealed class CategoryLookupAdapter : ICategoryLookupPort
{
    private readonly MarketplaceDbContext _context;

    public CategoryLookupAdapter(MarketplaceDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, short>> GetAllCategoryKeysAsync(
        CancellationToken ct = default)
    {
        List<(string Dimension, string Value, short Id)> rows = await _context.Categories
            .AsNoTracking()
            .Select(c => new ValueTuple<string, string, short>(c.Dimension, c.Value, c.Id))
            .ToListAsync(ct);

        Dictionary<string, short> lookup = new(rows.Count, StringComparer.Ordinal);

        foreach ((string dimension, string value, short id) in rows)
        {
            lookup[$"{dimension}:{value}"] = id;
        }

        return lookup;
    }
}
