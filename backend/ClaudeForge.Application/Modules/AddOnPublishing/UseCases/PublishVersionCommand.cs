namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Input command for publishing a new version to an existing plugin.
/// </summary>
public sealed record PublishVersionCommand(
    Guid PluginId,
    Stream PackageStream,
    string FileName,
    string Version,
    string ReleaseNotes);
