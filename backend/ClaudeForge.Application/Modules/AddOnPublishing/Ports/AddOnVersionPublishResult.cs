namespace ClaudeForge.Application.Modules.AddOnPublishing.Ports;

/// <summary>
/// Result returned after successfully publishing a new version to an existing plugin.
/// </summary>
public sealed record AddOnVersionPublishResult(Guid PluginId, Guid VersionId, string Version);
