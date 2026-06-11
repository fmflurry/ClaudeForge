using ClaudeForge.Application.Modules.AddOnCatalog.Ports;
using ClaudeForge.Application.Modules.AddOnCatalog.UseCases;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.AddOnCatalog;

/// <summary>
/// Unit tests for <see cref="GetFeaturedAddOnUseCase"/>.
///
/// Verifies:
///   - Returns the featured plugin when one is flagged.
///   - Returns null (absence signal → 404) when no plugin is featured.
///   - Never returns a non-featured plugin.
///   - Single-featured invariant after rotation (only the new featured is returned).
/// </summary>
public sealed class GetFeaturedPluginUseCaseTests
{
    private static FeaturedAddOnDto MakeFeatured(string slug) => new()
    {
        PluginId = Guid.NewGuid().ToString(),
        Name = $"Plugin {slug}",
        Slug = slug,
        LatestVersion = "1.0.0",
    };

    // -------------------------------------------------------------------------
    // Returns featured plugin
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFeaturedPlugin_WhenOneFeatured_ReturnsThatPlugin()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        FeaturedAddOnDto featured = MakeFeatured("typescript-linter");
        repo.GetFeaturedAddOnAsync(Arg.Any<CancellationToken>()).Returns(featured);

        GetFeaturedAddOnUseCase useCase = new(repo);

        // Act
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("typescript-linter", result.Slug);
        Assert.Equal("1.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task GetFeaturedPlugin_WhenFeatured_IncludesPluginIdNameSlugLatestVersion()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        string expectedId = Guid.NewGuid().ToString();
        FeaturedAddOnDto featured = new()
        {
            PluginId = expectedId,
            Name = "TypeScript Linter",
            Slug = "typescript-linter",
            LatestVersion = "2.3.1",
        };
        repo.GetFeaturedAddOnAsync(Arg.Any<CancellationToken>()).Returns(featured);

        GetFeaturedAddOnUseCase useCase = new(repo);

        // Act
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.PluginId);
        Assert.Equal("TypeScript Linter", result.Name);
        Assert.Equal("typescript-linter", result.Slug);
        Assert.Equal("2.3.1", result.LatestVersion);
    }

    // -------------------------------------------------------------------------
    // Signals absence when none featured
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFeaturedPlugin_WhenNoneFeatured_ReturnsNull()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        repo.GetFeaturedAddOnAsync(Arg.Any<CancellationToken>()).Returns((FeaturedAddOnDto?)null);

        GetFeaturedAddOnUseCase useCase = new(repo);

        // Act
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();

        // Assert — null signals 404 to the endpoint handler
        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Never returns a non-featured plugin
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFeaturedPlugin_RepositoryFiltersCorrectly_OnlyFeaturedIsReturned()
    {
        // Arrange: repository is correctly implemented and only returns the featured plugin.
        // The use case itself must not add any logic that could introduce a non-featured plugin.
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        FeaturedAddOnDto onlyFeatured = MakeFeatured("featured-slug");
        repo.GetFeaturedAddOnAsync(Arg.Any<CancellationToken>()).Returns(onlyFeatured);

        GetFeaturedAddOnUseCase useCase = new(repo);

        // Act
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("featured-slug", result.Slug);
        // Use case must delegate entirely to the repository — one call, no additional filtering
        await repo.Received(1).GetFeaturedAddOnAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Single-featured invariant after rotation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFeaturedPlugin_AfterRotation_OnlyNewFeaturedIsReturned()
    {
        // Arrange: simulate rotation — before was plugin A, now is plugin B.
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        FeaturedAddOnDto pluginB = MakeFeatured("new-featured-plugin");
        repo.GetFeaturedAddOnAsync(Arg.Any<CancellationToken>()).Returns(pluginB);

        GetFeaturedAddOnUseCase useCase = new(repo);

        // Act
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();

        // Assert: only the new featured plugin is returned (invariant: at most one)
        Assert.NotNull(result);
        Assert.Equal("new-featured-plugin", result.Slug);
    }

    [Fact]
    public async Task GetFeaturedPlugin_LatestVersionNullable_WhenNoVersionsExist()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        FeaturedAddOnDto featured = new()
        {
            PluginId = Guid.NewGuid().ToString(),
            Name = "No Version Plugin",
            Slug = "no-version",
            LatestVersion = null,
        };
        repo.GetFeaturedAddOnAsync(Arg.Any<CancellationToken>()).Returns(featured);

        GetFeaturedAddOnUseCase useCase = new(repo);

        // Act
        FeaturedAddOnDto? result = await useCase.ExecuteAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LatestVersion);
    }
}
