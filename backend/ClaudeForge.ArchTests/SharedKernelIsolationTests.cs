using System.Reflection;
using NetArchTest.Rules;

namespace ClaudeForge.ArchTests;

/// <summary>
/// Architecture enforcement tests for Group 2, Task 2.5 — Shared-Kernel Authorization Seam.
///
/// These tests are RED because the shared-kernel contracts do not yet exist.
/// The coder MUST create them to make these tests GREEN.
///
/// Rules enforced:
///   (a) Existing module Core namespaces (PluginCatalog / PluginSearch /
///       PluginDistribution / PluginPublishing) must NOT depend on the
///       Identity or Organizations namespaces.
///   (b) IOrgMembershipQueryPort's method signatures expose only primitive
///       types (Guid / bool) — no Organizations domain types.
///
/// Pattern mirrors CoreIsolationTests.cs exactly:
///   Types.InAssembly(...).That().ResideInNamespace(...).ShouldNot()
///       .HaveDependencyOn(...).GetResult()
/// </summary>
public sealed class SharedKernelIsolationTests
{
    // Namespaces that existing marketplace modules must never reference.
    // Corrected from "ClaudeForge.Core.Modules.Identity" → actual namespace (no ".Modules." segment).
    private const string IdentityModuleNamespace = "ClaudeForge.Core.Identity";
    // Corrected from "ClaudeForge.Core.Modules.Organizations" → actual EF entity namespace
    // (there is no Core Organizations domain layer yet; the org entities live in Infrastructure.Persistence.Entities).
    private const string OrganizationsModuleNamespace = "ClaudeForge.Infrastructure.Persistence.Entities";

    // Shared-kernel authZ contracts are defined here — allowed cross-module dependency
    private const string SharedAuthzNamespace = "ClaudeForge.Core.Shared.Authorization";

    // Existing module Application-layer namespaces that must remain clean
    private const string PluginCatalogNamespace = "ClaudeForge.Application.Modules.PluginCatalog";
    private const string PluginSearchNamespace = "ClaudeForge.Application.Modules.PluginSearch";
    private const string PluginDistributionNamespace = "ClaudeForge.Application.Modules.PluginDistribution";
    private const string PluginPublishingNamespace = "ClaudeForge.Application.Modules.PluginPublishing";

    // =========================================================================
    // (a) Existing modules must NOT depend on Identity or Organizations namespaces
    // =========================================================================

    [Fact]
    public void PluginCatalog_Core_ShouldNot_HaveDependencyOnIdentityModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginCatalog.UseCases.ListPluginsUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginCatalogNamespace)
            .ShouldNot()
            .HaveDependencyOn(IdentityModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginCatalog must not depend on Identity module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginCatalog_Core_ShouldNot_HaveDependencyOnOrganizationsModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginCatalog.UseCases.ListPluginsUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginCatalogNamespace)
            .ShouldNot()
            .HaveDependencyOn(OrganizationsModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginCatalog must not depend on Organizations module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginSearch_Core_ShouldNot_HaveDependencyOnIdentityModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginSearch.UseCases.SearchPluginsUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginSearchNamespace)
            .ShouldNot()
            .HaveDependencyOn(IdentityModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginSearch must not depend on Identity module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginSearch_Core_ShouldNot_HaveDependencyOnOrganizationsModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginSearch.UseCases.SearchPluginsUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginSearchNamespace)
            .ShouldNot()
            .HaveDependencyOn(OrganizationsModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginSearch must not depend on Organizations module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginDistribution_Core_ShouldNot_HaveDependencyOnIdentityModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginDistribution.UseCases.DownloadPluginUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginDistributionNamespace)
            .ShouldNot()
            .HaveDependencyOn(IdentityModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginDistribution must not depend on Identity module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginDistribution_Core_ShouldNot_HaveDependencyOnOrganizationsModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginDistribution.UseCases.DownloadPluginUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginDistributionNamespace)
            .ShouldNot()
            .HaveDependencyOn(OrganizationsModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginDistribution must not depend on Organizations module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginPublishing_Core_ShouldNot_HaveDependencyOnIdentityModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginPublishing.UseCases.UploadPluginUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginPublishingNamespace)
            .ShouldNot()
            .HaveDependencyOn(IdentityModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginPublishing must not depend on Identity module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void PluginPublishing_Core_ShouldNot_HaveDependencyOnOrganizationsModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Application.Modules.PluginPublishing.UseCases.UploadPluginUseCase).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(PluginPublishingNamespace)
            .ShouldNot()
            .HaveDependencyOn(OrganizationsModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"PluginPublishing must not depend on Organizations module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // =========================================================================
    // Shared authZ namespace must NOT itself depend on Identity or Organizations
    // =========================================================================

    [Fact]
    public void SharedAuthzKernel_ShouldNot_HaveDependencyOnIdentityModule()
    {
        // The shared kernel (ICurrentUser, IOrgMembershipQueryPort, IPluginAccessPolicy)
        // must remain a pure-primitive seam — no Identity module dependency.
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Core.Shared.Exceptions.ProblemDetailsException).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(SharedAuthzNamespace)
            .ShouldNot()
            .HaveDependencyOn(IdentityModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Shared authZ kernel must not depend on Identity module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void SharedAuthzKernel_ShouldNot_HaveDependencyOnOrganizationsModule()
    {
        Types? types = Types.InAssembly(
            typeof(ClaudeForge.Core.Shared.Exceptions.ProblemDetailsException).Assembly);

        TestResult? result = types
            .That()
            .ResideInNamespace(SharedAuthzNamespace)
            .ShouldNot()
            .HaveDependencyOn(OrganizationsModuleNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Shared authZ kernel must not depend on Organizations module. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // =========================================================================
    // (b) Reflection-based: IOrgMembershipQueryPort method signatures use
    //     only primitive/BCL types — no Organizations domain types
    //
    //     NetArchTest cannot inspect method signatures directly, so we use
    //     reflection to assert type constraints on the port interface.
    // =========================================================================

    [Fact]
    public void IOrgMembershipQueryPort_AllParameters_AreOnlyPrimitiveOrBclTypes()
    {
        // This test references the type that does not exist yet — RED.
        // Once the coder creates ClaudeForge.Core.Shared.Authorization.IOrgMembershipQueryPort,
        // this test will compile and must pass.
        Type portType = typeof(ClaudeForge.Core.Shared.Authorization.IOrgMembershipQueryPort);
        MethodInfo[] methods = portType.GetMethods();

        Assert.True(methods.Length > 0, "IOrgMembershipQueryPort must declare at least one method");

        foreach (MethodInfo method in methods)
        {
            foreach (ParameterInfo param in method.GetParameters())
            {
                Type paramType = param.ParameterType;
                AssertIsAllowedType(paramType, method.Name, param.Name ?? "?");
            }
        }
    }

    [Fact]
    public void IOrgMembershipQueryPort_AllReturnTypes_AreOnlyPrimitiveOrBclTypes()
    {
        Type portType = typeof(ClaudeForge.Core.Shared.Authorization.IOrgMembershipQueryPort);
        MethodInfo[] methods = portType.GetMethods();

        foreach (MethodInfo method in methods)
        {
            // Task<T> — inspect the T
            Type returnType = method.ReturnType;
            if (returnType.IsGenericType)
            {
                foreach (Type arg in returnType.GetGenericArguments())
                {
                    // Guid[] → element type is Guid; bool[]? → bool
                    Type leafType = arg.IsArray ? arg.GetElementType()! : arg;
                    AssertIsAllowedType(leafType, method.Name, "return");
                }
            }
        }
    }

    // =========================================================================
    // Helper — asserts a type is a BCL/primitive type only (Guid, bool, string,
    // CancellationToken, int, nullables thereof)
    // =========================================================================

    private static readonly HashSet<Type> AllowedTypes = new()
    {
        typeof(Guid),
        typeof(bool),
        typeof(string),
        typeof(int),
        typeof(long),
        typeof(System.Threading.CancellationToken),
    };

    private static void AssertIsAllowedType(Type type, string methodName, string paramName)
    {
        // Allow arrays of allowed types
        if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            AssertIsAllowedType(elementType, methodName, $"{paramName}[]");
            return;
        }

        // Allow Nullable<T> where T is allowed
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            AssertIsAllowedType(underlying, methodName, $"Nullable<{paramName}>");
            return;
        }

        bool isAllowed =
            AllowedTypes.Contains(type) ||
            type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(Guid) ||
            type == typeof(System.Threading.CancellationToken);

        Assert.True(isAllowed,
            $"IOrgMembershipQueryPort.{methodName} uses non-primitive type '{type.FullName}' " +
            $"for '{paramName}'. Only Guid/bool/string/CancellationToken/primitives are allowed " +
            $"per design.md §3 ('return primitives only').");
    }
}
