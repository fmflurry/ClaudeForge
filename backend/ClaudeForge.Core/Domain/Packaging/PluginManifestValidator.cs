using ClaudeForge.Core.Domain.Plugins;
using FluentValidation;

namespace ClaudeForge.Core.Domain.Packaging;

/// <summary>
/// FluentValidation validator for the canonical <see cref="PluginManifest"/> schema.
///
/// Rules:
///   Name        — required, 1–128 chars
///   Version     — required, delegates to <see cref="SemVer.Parse"/> (throws ArgumentException on invalid)
///   Description — required, non-empty, &lt;500 chars
///   Author      — required, non-empty, not whitespace-only
///   Types       — required, ≥1, each in { skill, hook, agent, command, plugin }
///   Languages   — required, ≥1, each non-empty
///   UseCaseTags — optional; when provided, each in controlled vocab
///   Entrypoints — optional, no inner validation required for MVP
///   Dependencies— optional, no inner validation required for MVP
///   License     — optional; null treated as MIT (no length constraint enforced)
///   DocsUrl     — optional
///   Readme      — optional
/// </summary>
public sealed class PluginManifestValidator : AbstractValidator<PluginManifest>
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.Ordinal)
    {
        "skill", "hook", "agent", "command", "plugin",
    };

    private static readonly HashSet<string> ValidUseCaseTags = new(StringComparer.Ordinal)
    {
        "dev-team", "product-owner", "product-manager", "devops", "security", "data-analyst",
    };

    public PluginManifestValidator()
    {
        RuleFor(m => m.Name)
            .NotNull()
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(m => m.Version)
            .NotNull()
            .NotEmpty()
            .Must(BeValidSemVer)
            .WithMessage("Version must be a valid semantic version (e.g., 1.0.0)");

        RuleFor(m => m.Description)
            .NotNull()
            .NotEmpty()
            .Must(d => d is not null && d.Length < 500)
            .WithMessage("Description must be less than 500 characters");

        RuleFor(m => m.Author)
            .NotNull()
            .NotEmpty()
            .Must(a => a is not null && !string.IsNullOrWhiteSpace(a))
            .WithMessage("Author must not be empty or whitespace");

        RuleFor(m => m.Types)
            .NotNull()
            .Must(t => t is not null && t.Length >= 1)
            .WithMessage("Types must contain at least one entry");

        When(m => m.Types is not null && m.Types.Length >= 1, () =>
        {
            RuleForEach(m => m.Types)
                .Must(t => ValidTypes.Contains(t))
                .WithMessage("Each type must be one of: skill, hook, agent, command, plugin");
        });

        RuleFor(m => m.Languages)
            .NotNull()
            .Must(l => l is not null && l.Length >= 1)
            .WithMessage("Languages must contain at least one entry");

        When(m => m.Languages is not null && m.Languages.Length >= 1, () =>
        {
            RuleForEach(m => m.Languages)
                .NotEmpty()
                .WithMessage("Each language entry must be non-empty");
        });

        When(m => m.UseCaseTags is not null && m.UseCaseTags.Length > 0, () =>
        {
            RuleForEach(m => m.UseCaseTags!)
                .Must(tag => ValidUseCaseTags.Contains(tag))
                .WithMessage(
                    "Each useCaseTag must be one of: dev-team, product-owner, product-manager, devops, security, data-analyst");
        });
    }

    private static bool BeValidSemVer(string? version)
    {
        if (string.IsNullOrEmpty(version))
            return false;

        try
        {
            SemVer.Parse(version);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
