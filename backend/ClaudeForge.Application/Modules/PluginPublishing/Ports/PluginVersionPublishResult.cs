namespace ClaudeForge.Application.Modules.PluginPublishing.Ports;

/// <summary>
/// Result returned after successfully publishing a new version to an existing plugin.
/// </summary>
public sealed record PluginVersionPublishResult(Guid PluginId, Guid VersionId, string Version);
