namespace ClaudeForge.Application.Modules.AddOnPublishing.UseCases;

/// <summary>
/// Input command for uploading a new plugin with its initial version.
/// </summary>
public sealed record UploadAddOnCommand(
    Stream PackageStream,
    string FileName,
    string Name,
    string Description,
    string Author,
    string InitialVersion,
    string ReleaseNotes,
    string Visibility = "public",
    Guid? OwnerOrgId = null,
    Guid? OwnerUserId = null,
    IReadOnlyList<string>? Types = null,
    IReadOnlyList<string>? Languages = null,
    IReadOnlyList<string>? UseCaseTags = null);
