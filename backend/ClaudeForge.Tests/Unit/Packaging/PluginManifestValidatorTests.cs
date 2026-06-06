using ClaudeForge.Core.Domain.Packaging;
using FluentValidation;
using FluentValidation.Results;

namespace ClaudeForge.Tests.Unit.Packaging;

/// <summary>
/// Unit tests for the canonical plugin manifest schema model and its FluentValidation validator
/// (task 3.6).
///
/// Expected production types (coder must match these names exactly):
///
///   ClaudeForge.Core.Domain.Packaging.PluginManifest
///     string   Name           — required, 1–128 chars
///     string   Version        — required, must parse as SemVer
///     string   Description    — required, non-empty, &lt;500 chars
///     string   Author         — required, non-empty
///     string[] Types          — required, ≥1 item, each in { skill, hook, agent, command, plugin }
///     string[] Languages      — required, ≥1 item (non-empty string list; no enum restriction in model)
///     string[] UseCaseTags    — optional (empty by default); each item in:
///                               { dev-team, product-owner, product-manager, devops, security, data-analyst }
///     PluginEntrypoint[] Entrypoints — optional
///     Dictionary&lt;string,string&gt; Dependencies — optional
///     string   License        — optional, defaults to "MIT" when absent/null
///     string?  DocsUrl        — optional
///     string?  Readme         — optional
///
///   ClaudeForge.Core.Domain.Packaging.PluginEntrypoint
///     string Name
///     string Description
///     string Signature
///
///   ClaudeForge.Core.Domain.Packaging.PluginManifestValidator : AbstractValidator&lt;PluginManifest&gt;
///     (parameterless constructor)
///
///   Version validation delegates to ClaudeForge.Core.Domain.Plugins.SemVer.Parse —
///   do NOT re-implement semver; reuse the existing value object.
/// </summary>
public sealed class PluginManifestValidatorTests
{
    private readonly IValidator<PluginManifest> _validator = new PluginManifestValidator();

    // =========================================================================
    // Helper: build a fully valid manifest
    // =========================================================================

    private static PluginManifest ValidManifest() => new()
    {
        Name = "my-awesome-plugin",
        Version = "1.0.0",
        Description = "Does something very useful for the whole team.",
        Author = "Jane Doe",
        Types = ["skill"],
        Languages = ["typescript"],
        UseCaseTags = [],
        Entrypoints = [],
        Dependencies = new Dictionary<string, string>(),
        License = "MIT",
        DocsUrl = null,
        Readme = null,
    };

    // =========================================================================
    // Group A — happy path: fully valid manifest passes validation
    // =========================================================================

    [Fact]
    public void Validate_FullyValidManifest_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest();

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Validate_AllOptionalFieldsPopulated_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with
        {
            Types = ["skill", "hook"],
            Languages = ["typescript", "python"],
            UseCaseTags = ["dev-team", "devops"],
            Entrypoints =
            [
                new PluginEntrypoint
                {
                    Name = "main",
                    Description = "Primary entrypoint",
                    Signature = "main(ctx: Context): Promise<void>",
                },
            ],
            Dependencies = new Dictionary<string, string> { ["lodash"] = ">=4.0.0" },
            License = "Apache-2.0",
            DocsUrl = "https://docs.example.com",
            Readme = "# My Awesome Plugin",
        };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    // =========================================================================
    // Group B — required field: Name
    // =========================================================================

    [Fact]
    public void Validate_NullName_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Name = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Name));
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Name = "" };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Name));
    }

    [Fact]
    public void Validate_NameExactly128Chars_Passes()
    {
        // Arrange — boundary: exactly 128 characters must be valid
        PluginManifest manifest = ValidManifest() with { Name = new string('a', 128) };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "Name of exactly 128 characters must be valid");
    }

    [Fact]
    public void Validate_NameLongerThan128Chars_Fails()
    {
        // Arrange — boundary: 129 characters must fail
        PluginManifest manifest = ValidManifest() with { Name = new string('a', 129) };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Name));
    }

    [Fact]
    public void Validate_NameSingleChar_Passes()
    {
        // Arrange — boundary: 1 character is the minimum
        PluginManifest manifest = ValidManifest() with { Name = "x" };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "Name of exactly 1 character must be valid");
    }

    // =========================================================================
    // Group C — required field: Version (must be valid semver)
    // =========================================================================

    [Fact]
    public void Validate_NullVersion_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Version = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Version));
    }

    [Theory]
    [InlineData("1.0")]         // missing patch
    [InlineData("1")]           // missing minor + patch
    [InlineData("a.b.c")]       // non-integer parts
    [InlineData("1.2.3.4")]     // extra segment
    [InlineData("not-a-version")]
    [InlineData("")]
    public void Validate_InvalidSemver_Fails(string version)
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Version = version };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid, $"Version '{version}' should be invalid");
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Version));
    }

    [Theory]
    [InlineData("0.0.0")]
    [InlineData("1.0.0")]
    [InlineData("10.20.30")]
    [InlineData("255.255.255")]
    public void Validate_ValidSemver_Passes(string version)
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Version = version };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, $"Version '{version}' should be valid");
    }

    // =========================================================================
    // Group D — required field: Description
    // =========================================================================

    [Fact]
    public void Validate_NullDescription_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Description = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Description));
    }

    [Fact]
    public void Validate_EmptyDescription_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Description = "" };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Description));
    }

    [Fact]
    public void Validate_DescriptionExactly499Chars_Passes()
    {
        // Arrange — boundary: 499 characters must pass
        PluginManifest manifest = ValidManifest() with { Description = new string('x', 499) };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "Description of 499 characters must be valid");
    }

    [Fact]
    public void Validate_DescriptionExactly500Chars_Fails()
    {
        // Arrange — boundary: exactly 500 characters must fail (constraint is <500)
        PluginManifest manifest = ValidManifest() with { Description = new string('x', 500) };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid, "Description of exactly 500 characters must be invalid (must be <500)");
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Description));
    }

    // =========================================================================
    // Group E — required field: Author
    // =========================================================================

    [Fact]
    public void Validate_NullAuthor_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Author = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Author));
    }

    [Fact]
    public void Validate_EmptyAuthor_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Author = "" };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Author));
    }

    [Fact]
    public void Validate_WhitespaceOnlyAuthor_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Author = "   " };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Author));
    }

    // =========================================================================
    // Group F — required field: Types[] (≥1 item, values in enum)
    // =========================================================================

    [Fact]
    public void Validate_NullTypes_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Types = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Types));
    }

    [Fact]
    public void Validate_EmptyTypesArray_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Types = [] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Types));
    }

    [Theory]
    [InlineData("skill")]
    [InlineData("hook")]
    [InlineData("agent")]
    [InlineData("command")]
    [InlineData("plugin")]
    public void Validate_EachValidTypeValue_Passes(string type)
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Types = [type] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, $"Type '{type}' must be valid");
    }

    [Theory]
    [InlineData("integration")]    // old spec value — rejected in canonical schema
    [InlineData("utility")]        // old spec value — rejected
    [InlineData("other")]          // old spec value — rejected
    [InlineData("SKILL")]          // wrong casing
    [InlineData("unknown-type")]
    [InlineData("")]
    public void Validate_InvalidTypeValue_Fails(string invalidType)
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Types = [invalidType] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid, $"Type '{invalidType}' should be invalid");
    }

    [Fact]
    public void Validate_MultipleValidTypes_Passes()
    {
        // Arrange — a plugin can have multiple types
        PluginManifest manifest = ValidManifest() with { Types = ["skill", "hook", "command"] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "Multiple valid types must be accepted");
    }

    // =========================================================================
    // Group G — required field: Languages[] (≥1, non-empty strings)
    // =========================================================================

    [Fact]
    public void Validate_NullLanguages_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Languages = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Languages));
    }

    [Fact]
    public void Validate_EmptyLanguagesArray_Fails()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Languages = [] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(PluginManifest.Languages));
    }

    [Theory]
    [InlineData("typescript")]
    [InlineData("python")]
    [InlineData("go")]
    [InlineData("rust")]
    public void Validate_SeededLanguageValues_Pass(string language)
    {
        // Arrange — seeded vocab values must all pass
        PluginManifest manifest = ValidManifest() with { Languages = [language] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, $"Language '{language}' must be valid");
    }

    [Fact]
    public void Validate_MultipleLanguages_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Languages = ["typescript", "python"] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid);
    }

    // =========================================================================
    // Group H — optional field: UseCaseTags[] (subset of controlled vocab)
    // =========================================================================

    [Fact]
    public void Validate_NullUseCaseTags_Passes()
    {
        // Arrange — null (absent) is treated as empty; no error
        PluginManifest manifest = ValidManifest() with { UseCaseTags = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "null UseCaseTags must be valid (field is optional)");
    }

    [Fact]
    public void Validate_EmptyUseCaseTags_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { UseCaseTags = [] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("dev-team")]
    [InlineData("product-owner")]
    [InlineData("product-manager")]
    [InlineData("devops")]
    [InlineData("security")]
    [InlineData("data-analyst")]
    public void Validate_EachValidUseCaseTag_Passes(string tag)
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { UseCaseTags = [tag] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, $"UseCaseTag '{tag}' must be valid");
    }

    [Theory]
    [InlineData("auth")]          // old spec tag — not in canonical vocab
    [InlineData("marketing")]
    [InlineData("dev_team")]      // underscore instead of hyphen
    [InlineData("DEV-TEAM")]      // wrong casing
    [InlineData("")]
    public void Validate_InvalidUseCaseTag_Fails(string tag)
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { UseCaseTags = [tag] };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.False(result.IsValid, $"UseCaseTag '{tag}' should be invalid");
    }

    // =========================================================================
    // Group I — optional field: License defaults to "MIT"
    // =========================================================================

    [Fact]
    public void Validate_NullLicense_Passes_AndDefaultsToMit()
    {
        // Arrange — null license should default to "MIT"
        PluginManifest manifest = ValidManifest() with { License = null };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert — valid (MIT default)
        Assert.True(result.IsValid, "Null license must be valid; MIT is the default");
    }

    [Fact]
    public void Validate_ExplicitMitLicense_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { License = "MIT" };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OtherLicenseStrings_Pass()
    {
        // Arrange — any non-empty license string is acceptable
        PluginManifest manifest = ValidManifest() with { License = "Apache-2.0" };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid);
    }

    // =========================================================================
    // Group J — optional fields: Entrypoints[]
    // =========================================================================

    [Fact]
    public void Validate_NullEntrypoints_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Entrypoints = null! };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "Null entrypoints must be valid (field is optional)");
    }

    [Fact]
    public void Validate_ValidEntrypoints_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with
        {
            Entrypoints =
            [
                new PluginEntrypoint
                {
                    Name = "run",
                    Description = "Execute the plugin",
                    Signature = "run(input: string): string",
                },
            ],
        };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid);
    }

    // =========================================================================
    // Group K — optional fields: Dependencies{}
    // =========================================================================

    [Fact]
    public void Validate_NullDependencies_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with { Dependencies = null };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid, "Null dependencies must be valid (field is optional)");
    }

    [Fact]
    public void Validate_DependenciesWithVersionSpec_Passes()
    {
        // Arrange
        PluginManifest manifest = ValidManifest() with
        {
            Dependencies = new Dictionary<string, string>
            {
                ["lodash"] = "^4.17.0",
                ["axios"] = ">=1.0.0",
            },
        };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert
        Assert.True(result.IsValid);
    }

    // =========================================================================
    // Group L — multiple violations reported simultaneously
    // =========================================================================

    [Fact]
    public void Validate_MultipleViolations_ReportsAllErrors()
    {
        // Arrange — manifest with several violations at once
        PluginManifest manifest = new()
        {
            Name = "",
            Version = "bad-version",
            Description = "",
            Author = "",
            Types = [],
            Languages = [],
        };

        // Act
        ValidationResult result = _validator.Validate(manifest);

        // Assert — at least 5 distinct property errors
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5,
            $"Expected at least 5 validation errors but got {result.Errors.Count}: "
            + string.Join("; ", result.Errors.Select(e => e.PropertyName + ": " + e.ErrorMessage)));
    }
}
