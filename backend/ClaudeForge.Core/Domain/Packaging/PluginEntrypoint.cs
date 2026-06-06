namespace ClaudeForge.Core.Domain.Packaging;

/// <summary>
/// Describes a single callable entrypoint exposed by a plugin.
/// </summary>
public sealed record PluginEntrypoint
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Signature { get; init; }
}
