using ClaudeForge.Application.Modules.AddOnCatalog.Ports;
using ClaudeForge.Application.Modules.AddOnCatalog.UseCases;
using ClaudeForge.Core.Shared.Model;
using NSubstitute;

namespace ClaudeForge.Tests.Unit.AddOnCatalog;

/// <summary>
/// Unit tests for Group 4: ListAddOnsUseCase, GetAddOnDetailsUseCase, ListCategoriesUseCase.
///
/// Uses NSubstitute mocks — no real database.
///
/// Expected production types:
///
///   ClaudeForge.Application.Modules.AddOnCatalog.UseCases.ListAddOnsUseCase
///     ListAddOnsUseCase(IAddOnRepositoryPort repository)
///     Task&lt;PaginatedEnvelope&lt;AddOnSummaryDto&gt;&gt; ExecuteAsync(ListAddOnsQuery query, CancellationToken ct = default)
///
///   ClaudeForge.Application.Modules.AddOnCatalog.UseCases.GetAddOnDetailsUseCase
///     GetAddOnDetailsUseCase(IAddOnRepositoryPort repository)
///     Task&lt;AddOnDetailDto&gt; ExecuteAsync(Guid pluginId, CancellationToken ct = default)
///     throws AddOnNotFoundException when plugin is missing
///
///   ClaudeForge.Application.Modules.AddOnCatalog.UseCases.ListCategoriesUseCase
///     ListCategoriesUseCase(ICategoryRepositoryPort repository)
///     Task&lt;CategoryListDto&gt; ExecuteAsync(CancellationToken ct = default)
///
///   ClaudeForge.Application.Modules.AddOnCatalog.UseCases.AddOnNotFoundException
///     AddOnNotFoundException : ProblemDetailsException — message "Plugin not found" — maps to 404
///
///   ClaudeForge.Application.Modules.AddOnCatalog.UseCases.InvalidCategoryException
///     InvalidCategoryException : ProblemDetailsException — maps to 400
///
///   ClaudeForge.Application.Modules.AddOnCatalog.UseCases.ListAddOnsQuery
///     int Page = 1, int Limit = 20, string SortKey = "createdAt", string SortOrder = "desc"
///     IReadOnlyList&lt;string&gt;? TypeFilter, LanguageFilter, UseCaseFilter
/// </summary>
public sealed class PluginCatalogUseCaseTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AddOnSummaryDto MakeSummary(string name) => new()
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

    private static AddOnDetailDto MakeDetail(string name, IReadOnlyList<AddOnVersionDto>? versions = null) => new()
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
    // ListAddOnsUseCase
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPlugins_EmptySystem_ReturnsEmptyEnvelope()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        repo.ListAddOnsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: (IReadOnlyList<AddOnSummaryDto>)[], TotalCount: 0));

        ListAddOnsUseCase useCase = new(repo);
        ListAddOnsQuery query = new();

        // Act
        PaginatedEnvelope<AddOnSummaryDto> result = await useCase.ExecuteAsync(query);

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
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        // 3 total plugins, but page 100 returns empty items
        repo.ListAddOnsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: (IReadOnlyList<AddOnSummaryDto>)[], TotalCount: 3));

        ListAddOnsUseCase useCase = new(repo);
        ListAddOnsQuery query = new() { Page = 100, Limit = 20 };

        // Act
        PaginatedEnvelope<AddOnSummaryDto> result = await useCase.ExecuteAsync(query);

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
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        repo.ListAddOnsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: (IReadOnlyList<AddOnSummaryDto>)[MakeSummary("Plugin1")], TotalCount: 1));

        ListAddOnsUseCase useCase = new(repo);
        // "invalid_field" is not a valid sort key — use case must NOT throw
        ListAddOnsQuery query = new() { SortKey = "invalid_field" };

        // Act — should not throw
        PaginatedEnvelope<AddOnSummaryDto> result = await useCase.ExecuteAsync(query);

        // Assert — execution succeeded with fallback behavior
        Assert.NotNull(result);

        // Verify repository was called with a safe (valid) sort key — the fallback "createdAt"
        await repo.Received(1).ListAddOnsAsync(
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
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        ListAddOnsUseCase useCase = new(repo);

        // "FORTRAN" is not a valid language category
        ListAddOnsQuery query = new() { LanguageFilter = ["FORTRAN"] };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCategoryException>(
            () => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ListPlugins_InvalidType_ThrowsInvalidCategoryException()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        ListAddOnsUseCase useCase = new(repo);

        // "widget" is not a valid type
        ListAddOnsQuery query = new() { TypeFilter = ["widget"] };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCategoryException>(
            () => useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ListPlugins_ValidQuery_PassesPaginationToRepository()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        IReadOnlyList<AddOnSummaryDto> returnedItems = [MakeSummary("P1"), MakeSummary("P2")];
        repo.ListAddOnsAsync(Arg.Any<PaginationRequest>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<IReadOnlyList<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns((Items: returnedItems, TotalCount: 10));

        ListAddOnsUseCase useCase = new(repo);
        ListAddOnsQuery query = new() { Page = 2, Limit = 5, SortKey = "downloads", SortOrder = "desc" };

        // Act
        PaginatedEnvelope<AddOnSummaryDto> result = await useCase.ExecuteAsync(query);

        // Assert
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.Limit);
        Assert.Equal(2, result.TotalPages); // ceil(10/5) = 2
    }

    // -------------------------------------------------------------------------
    // GetAddOnDetailsUseCase
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPluginDetails_UnknownId_ThrowsPluginNotFoundException()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        repo.GetAddOnByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AddOnDetailDto?)null);

        GetAddOnDetailsUseCase useCase = new(repo);
        Guid unknownId = Guid.NewGuid();

        // Act & Assert
        AddOnNotFoundException ex = await Assert.ThrowsAsync<AddOnNotFoundException>(
            () => useCase.ExecuteAsync(unknownId));

        Assert.Equal("Plugin not found", ex.Message);
    }

    [Fact]
    public async Task GetPluginDetails_ExistingPlugin_ReturnsDetail()
    {
        // Arrange
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        Guid pluginId = Guid.NewGuid();
        AddOnDetailDto detail = MakeDetail("MyPlugin", [
            new AddOnVersionDto
            {
                VersionNumber = "1.0.0",
                ReleaseDate = DateTimeOffset.UtcNow,
                ReleaseNotes = "Initial release",
                DownloadCount = 42,
                IsLatest = true,
            }
        ]);

        repo.GetAddOnByIdAsync(pluginId, Arg.Any<CancellationToken>())
            .Returns(detail);

        GetAddOnDetailsUseCase useCase = new(repo);

        // Act
        AddOnDetailDto result = await useCase.ExecuteAsync(pluginId);

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
        IAddOnRepositoryPort repo = Substitute.For<IAddOnRepositoryPort>();
        Guid pluginId = Guid.NewGuid();
        AddOnDetailDto detail = MakeDetail("NoVersionPlugin", []);

        repo.GetAddOnByIdAsync(pluginId, Arg.Any<CancellationToken>())
            .Returns(detail);

        GetAddOnDetailsUseCase useCase = new(repo);

        // Act
        AddOnDetailDto result = await useCase.ExecuteAsync(pluginId);

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
