using NetArchTest.Rules;
using Xunit;

namespace ClaudeForge.ArchTests;

/// <summary>
/// Architecture enforcement tests using NetArchTest.
/// These run in CI to enforce module isolation boundaries.
/// </summary>
public sealed class CoreIsolationTests
{
    private const string CoreNamespace = "ClaudeForge.Core";
    private const string InfrastructureNamespace = "ClaudeForge.Infrastructure";
    private const string ApplicationNamespace = "ClaudeForge.Application";

    [Fact]
    public void Core_ShouldNot_HaveAnyDependencyOnInfrastructure()
    {
        Types? types = Types.InAssembly(typeof(ClaudeForge.Core.Shared.Exceptions.ProblemDetailsException).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(CoreNamespace)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Core should not depend on Infrastructure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Core_ShouldNot_HaveDependencyOnEntityFramework()
    {
        Types? types = Types.InAssembly(typeof(ClaudeForge.Core.Shared.Exceptions.ProblemDetailsException).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(CoreNamespace)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Core should not depend on EF Core. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNot_HaveAnyDependencyOnInfrastructure()
    {
        Types? types = Types.InAssembly(typeof(ClaudeForge.Application.Modules.AddOnPublishing.UseCases.UploadAddOnUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(ApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application should not depend on Infrastructure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
