namespace ClaudeForge.Core.Domain.Packaging;

/// <summary>
/// Canonical, immutable representation of a plugin manifest (plugin.json / manifest.json).
/// All required fields are non-nullable strings or arrays.
/// Optional fields are nullable or default to empty arrays / null.
/// <see cref="EffectiveLicense"/> exposes the MIT default when <see cref="License"/> is null.
/// </summary>
public sealed record PluginManifest
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }

    /// <summary>Plugin type labels. Required, ≥1. Each value must be one of: skill, hook, agent, command, plugin.</summary>
    public required string[] Types { get; init; }

    /// <summary>Target/implementation languages. Required, ≥1.</summary>
    public required string[] Languages { get; init; }

    /// <summary>Use-case audience tags. Optional; each value must be in the controlled vocabulary.</summary>
    public string[]? UseCaseTags { get; init; }

    /// <summary>Declared entrypoints. Optional.</summary>
    public PluginEntrypoint[]? Entrypoints { get; init; }

    /// <summary>Declared dependencies (name → version constraint). Optional.</summary>
    public Dictionary<string, string>? Dependencies { get; init; }

    /// <summary>SPDX license identifier. Null is treated as MIT.</summary>
    public string? License { get; init; }

    /// <summary>Returns <see cref="License"/> when set, otherwise "MIT".</summary>
    public string EffectiveLicense => License ?? "MIT";

    public string? DocsUrl { get; init; }
    public string? Readme { get; init; }
}
