using ClaudeForge.Application.Modules.PluginCatalog.Ports;
using ClaudeForge.Application.Modules.PluginCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.PluginCatalog;

/// <summary>
/// Unit tests for Group 4: ListPluginsUseCase, GetPluginDetailsUseCase, ListCategoriesUseCase.
///
/// Uses NSubstitute mocks — no real database.
///
/// Expected production types:
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.ListPluginsUseCase
///     ListPluginsUseCase(IPluginRepositoryPort repository)
///     Task&lt;PaginatedEnvelope&lt;PluginSummaryDto&gt;&gt; ExecuteAsync(ListPluginsQuery query, CancellationToken ct = default)
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.GetPluginDetailsUseCase
///     GetPluginDetailsUseCase(IPluginRepositoryPort repository)
///     Task&lt;PluginDetailDto&gt; ExecuteAsync(Guid pluginId, CancellationToken ct = default)
///     throws PluginNotFoundException when plugin is missing
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.ListCategoriesUseCase
///     ListCategoriesUseCase(ICategoryRepositoryPort repository)
///     Task&lt;CategoryListDto&gt; ExecuteAsync(CancellationToken ct = default)
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.PluginNotFoundException
///     PluginNotFoundException : ProblemDetailsException — message "Plugin not found" — maps to 404
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.InvalidCategoryException
///     InvalidCategoryException : ProblemDetailsException — maps to 400
///
///   ClaudeForge.Application.Modules.PluginCatalog.UseCases.ListPluginsQuery
///     int Page = 1, int Limit = 20, string SortKey = "createdAt", string SortOrder = "desc"
///     IReadOnlyList&lt;string&gt;? TypeFilter, LanguageFilter, UseCaseFilter
/// </summary>
public sealed class PluginCatalogUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PluginSummaryDto MakeSummary(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = name.ToLowerInvariant(),
        Description = $"Description of {name}",
        Author = "test-author",
        DownloadCount = 0,
        LatestVersion = null,
        CreatedAt = DateTimeOffset.UtcNow,
        Types = [],
        Languages = [],
        UseCaseTags = [],
    };

    private static PluginDetailDto MakeDetail(string name, IReadOnlyList<PluginVersionDto>? versions = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = name.ToLowerInvariant(),
        Description = $"Description of {name}",
        Author = "test-author",
        DownloadCount = 0,
        LatestVersion = versions?.FirstOrDefault(v => v.IsLatest)?.VersionNumber,
        CreatedAt = DateTimeOffset.UtcNow,
        Types = [],
        Languages = [],
        UseCaseTags = [],
        Versions = versions ?? [],
    };

    // -------------------------------------------------------------------------
    // ListPluginsUseCase
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPlugins_EmptySystem_ReturnsEmptyEnvelope()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        repo.ListPluginsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: (IReadOnlyList<PluginSummaryDto>)[], TotalCount: 0));

        ListPluginsUseCase useCase = new(repo);
        ListPluginsQuery query = new();

        // Act
        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task ListPlugins_PageBeyondRange_ReturnsEmptyDataWithCorrectTotalAndTotalPages()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        // 3 total plugins, but page 100 returns empty items
        repo.ListPluginsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: (IReadOnlyList<PluginSummaryDto>)[], TotalCount: 3));

        ListPluginsUseCase useCase = new(repo);
        ListPluginsQuery query = new() { Page = 100, Limit = 20 };

        // Act
        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Empty(result.Data);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(100, result.Page);
        // totalPages = ceil(3/20) = 1, which is < 100
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task ListPlugins_InvalidSortKey_DefaultsToCreatedAtDescWithoutThrowing()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        repo.ListPluginsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: (IReadOnlyList<PluginSummaryDto>)[MakeSummary("Plugin1")], TotalCount: 1));

        ListPluginsUseCase useCase = new(repo);
        // "invalid_field" is not a valid sort key — use case must NOT throw
        ListPluginsQuery query = new() { SortKey = "invalid_field" };

        // Act — should not throw
        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(query);

        // Assert — execution succeeded with fallback behavior
        Assert.NotNull(result);

        // Verify repository was called with a safe (valid) sort key — the fallback "createdAt"
        await repo.Received(1).ListPluginsAsync(
            Arg.Any<PaginationRequest>(),
            "createdAt",
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPlugins_InvalidCategory_ThrowsInvalidCategoryException()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        ListPluginsUseCase useCase = new(repo);

        // "FORTRAN" is not a valid language category
        ListPluginsQuery query = new() { LanguageFilter = ["FORTRAN"] };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCategoryException>(
            () => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ListPlugins_InvalidType_ThrowsInvalidCategoryException()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        ListPluginsUseCase useCase = new(repo);

        // "widget" is not a valid type
        ListPluginsQuery query = new() { TypeFilter = ["widget"] };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCategoryException>(
            () => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ListPlugins_ValidQuery_PassesPaginationToRepository()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        IReadOnlyList<PluginSummaryDto> returnedItems = [MakeSummary("P1"), MakeSummary("P2")];
        repo.ListPluginsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: returnedItems, TotalCount: 10));

        ListPluginsUseCase useCase = new(repo);
        ListPluginsQuery query = new() { Page = 2, Limit = 5, SortKey = "downloads", SortOrder = "desc" };

        // Act
        PaginatedEnvelope<PluginSummaryDto> result = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.Limit);
        Assert.Equal(2, result.TotalPages); // ceil(10/5) = 2
    }

    // -------------------------------------------------------------------------
    // GetPluginDetailsUseCase
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPluginDetails_UnknownId_ThrowsPluginNotFoundException()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        repo.GetPluginByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PluginDetailDto?)null);

        GetPluginDetailsUseCase useCase = new(repo);
        Guid unknownId = Guid.NewGuid();

        // Act & Assert
        PluginNotFoundException ex = await Assert.ThrowsAsync<PluginNotFoundException>(
            () => useCase.ExecuteAsync(unknownId));

        Assert.Equal("Plugin not found", ex.Message);
    }

    [Fact]
    public async Task GetPluginDetails_ExistingPlugin_ReturnsDetail()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        Guid pluginId = Guid.NewGuid();
        PluginDetailDto detail = MakeDetail("MyPlugin", [
            new PluginVersionDto
            {
                VersionNumber = "1.0.0",
                ReleaseDate = DateTimeOffset.UtcNow,
                ReleaseNotes = "Initial release",
                DownloadCount = 42,
                IsLatest = true,
            }
        ]);

        repo.GetPluginByIdAsync(pluginId, Arg.Any<CancellationToken>())
            .Returns(detail);

        GetPluginDetailsUseCase useCase = new(repo);

        // Act
        PluginDetailDto result = await useCase.ExecuteAsync(pluginId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MyPlugin", result.Name);
        Assert.Single(result.Versions);
        Assert.True(result.Versions[0].IsLatest);
    }

    [Fact]
    public async Task GetPluginDetails_PluginWithNoVersions_ReturnsNullLatestVersion()
    {
        // Arrange
        IPluginRepositoryPort repo = Substitute.For<IPluginRepositoryPort>();
        Guid pluginId = Guid.NewGuid();
        PluginDetailDto detail = MakeDetail("NoVersionPlugin", []);

        repo.GetPluginByIdAsync(pluginId, Arg.Any<CancellationToken>())
            .Returns(detail);

        GetPluginDetailsUseCase useCase = new(repo);

        // Act
        PluginDetailDto result = await useCase.ExecuteAsync(pluginId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Versions);
        Assert.Null(result.LatestVersion);
    }

    // -------------------------------------------------------------------------
    // ListCategoriesUseCase
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListCategories_ReturnsThreeDimensionsWithCounts()
    {
        // Arrange
        ICategoryRepositoryPort repo = Substitute.For<ICategoryRepositoryPort>();
        CategoryListDto categoryList = new()
        {
            Types = [
                new CategoryDto { Value = "skill", DisplayName = "Skill", Count = 3 },
                new CategoryDto { Value = "hook", DisplayName = "Hook", Count = 1 },
                new CategoryDto { Value = "agent", DisplayName = "Agent", Count = 2 },
            ],
            Languages = [
                new CategoryDto { Value = "typescript", DisplayName = "TypeScript", Count = 5 },
                new CategoryDto { Value = "python", DisplayName = "Python", Count = 2 },
            ],
            UseCases = [
                new CategoryDto { Value = "dev-team", DisplayName = "Development Team", Count = 4 },
                new CategoryDto { Value = "devops", DisplayName = "DevOps", Count = 1 },
                new CategoryDto { Value = "product-owner", DisplayName = "Product Owner", Count = 0 },
            ],
        };

        repo.GetAllCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(categoryList);

        ListCategoriesUseCase useCase = new(repo);

        // Act
        CategoryListDto result = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(3, result.Types.Count);
        Assert.Equal(2, result.Languages.Count);
        Assert.Equal(3, result.UseCases.Count);

        Assert.Contains(result.Types, c => c.Value == "skill" && c.Count == 3);
        Assert.Contains(result.Languages, c => c.Value == "typescript" && c.Count == 5);
        Assert.Contains(result.UseCases, c => c.Value == "dev-team" && c.Count == 4);
    }

    [Fact]
    public async Task ListCategories_EmptySystem_ReturnsEmptyListsForAllDimensions()
    {
        // Arrange
        ICategoryRepositoryPort repo = Substitute.For<ICategoryRepositoryPort>();
        repo.GetAllCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new CategoryListDto
            {
                Types = [],
                Languages = [],
                UseCases = [],
            });

        ListCategoriesUseCase useCase = new(repo);

        // Act
        CategoryListDto result = await useCase.ExecuteAsync();

        // Assert
        Assert.Empty(result.Types);
        Assert.Empty(result.Languages);
        Assert.Empty(result.UseCases);
    }

    [Fact]
    public async Task ListCategories_AllCategoriesHaveZeroCount_WhenNoPluginsExist()
    {
        // Arrange
        ICategoryRepositoryPort repo = Substitute.For<ICategoryRepositoryPort>();
        repo.GetAllCategoriesAsync(Arg.Any<CancellationToken>())
            .Returns(new CategoryListDto
            {
                Types = [
                    new CategoryDto { Value = "skill", Count = 0 },
                    new CategoryDto { Value = "hook", Count = 0 },
                ],
                Languages = [
                    new CategoryDto { Value = "typescript", Count = 0 },
                ],
                UseCases = [
                    new CategoryDto { Value = "dev-team", Count = 0 },
                ],
            });

        ListCategoriesUseCase useCase = new(repo);

        // Act
        CategoryListDto result = await useCase.ExecuteAsync();

        // Assert
        Assert.All(result.Types, c => Assert.Equal(0, c.Count));
        Assert.All(result.Languages, c => Assert.Equal(0, c.Count));
        Assert.All(result.UseCases, c => Assert.Equal(0, c.Count));
    }
}
