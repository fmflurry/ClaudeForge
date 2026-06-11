namespace ClaudeForge.Application.Modules.AddOnPublishing.Ports;

/// <summary>
/// Outgoing port for resolving category controlled-vocabulary values during plugin publishing.
/// Returns a map of (dimension, value) → categoryId so the repository adapter can write
/// <c>plugin_categories</c> rows without re-querying inside the transaction.
/// </summary>
public interface ICategoryLookupPort
{
    /// <summary>
    /// Returns all known (dimension, value) pairs as a lookup map.
    /// Key: <c>"dimension:value"</c> — Value: the category's short integer id.
    /// </summary>
    Task<IReadOnlyDictionary<string, short>> GetAllCategoryKeysAsync(CancellationToken ct = default);
}
