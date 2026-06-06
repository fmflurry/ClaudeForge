namespace ClaudeForge.Application.Modules.PluginPublishing.Ports;

/// <summary>
/// Result returned after successfully creating a plugin with its initial version.
/// </summary>
public sealed record PluginPublishResult(Guid PluginId, string Version);
