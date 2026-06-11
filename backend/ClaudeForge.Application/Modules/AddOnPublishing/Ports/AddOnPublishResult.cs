namespace ClaudeForge.Application.Modules.AddOnPublishing.Ports;

/// <summary>
/// Result returned after successfully creating a plugin with its initial version.
/// </summary>
public sealed record AddOnPublishResult(Guid PluginId, string Version);
