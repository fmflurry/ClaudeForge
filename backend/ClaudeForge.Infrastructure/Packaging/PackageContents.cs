namespace ClaudeForge.Infrastructure.Packaging;

/// <summary>
/// Contents extracted from a plugin archive.
/// </summary>
/// <param name="ManifestBytes">Raw bytes of the manifest file (plugin.json or manifest.json).</param>
/// <param name="ReadmeText">Text content of README.md at archive root, or <c>null</c> if absent.</param>
public sealed record PackageContents(byte[] ManifestBytes, string? ReadmeText);
