namespace ClaudeForge.Application.Modules.PluginPublishing.UseCases;

/// <summary>
/// Input command for uploading a new plugin with its initial version.
/// </summary>
public sealed record UploadPluginCommand(
    Stream PackageStream,
    string FileName,
    string Name,
    string Description,
    string Author,
    string InitialVersion,
    string ReleaseNotes);
