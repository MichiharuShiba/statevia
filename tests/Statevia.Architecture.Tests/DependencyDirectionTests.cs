using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Statevia.Architecture.Tests;

/// <summary>
/// クリーン・アーキテクチャの依存方向ルールを検証する。
/// <list type="bullet">
/// <item><c>core/*</c> → <c>infrastructure/*</c> 禁止</item>
/// <item><c>core/*</c> → <c>service/*</c> 禁止</item>
/// <item><c>infrastructure/*</c> → <c>service/*</c> 禁止</item>
/// </list>
/// </summary>
public sealed class DependencyDirectionTests
{
    private static readonly string[] InfrastructureNamespaces =
    [
        "Statevia.Infrastructure.Persistence",
        "Statevia.Infrastructure.Security",
        "Statevia.Infrastructure.Notification",
        "Statevia.Infrastructure.Modules",
        "Statevia.Infrastructure.Actions",
        "Statevia.Infrastructure.Common",
    ];

    private static readonly string[] ServiceNamespaces =
    [
        "Statevia.Service.Api",
        "Statevia.Service.ActionHost",
        "Statevia.Service.Cli",
    ];

    private static Assembly CoreEngineAssembly =>
        typeof(Core.Engine.Abstractions.IExecutionEngine).Assembly;

    private static Assembly CoreApplicationAssembly =>
        typeof(Core.Application.DependencyInjection.ApplicationServiceCollectionExtensions).Assembly;

    private static Assembly CoreApplicationContractsAssembly =>
        typeof(Core.Application.Contracts.NotFoundException).Assembly;

    private static Assembly CoreActionsAbstractionsAssembly =>
        typeof(Core.Actions.Abstractions.Catalog.IActionCatalog).Assembly;

    /// <summary>Core.Engine は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreEngine_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreEngineAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Engine → Infrastructure", result));
    }

    /// <summary>Core.Engine は Service に依存しない。</summary>
    [Fact]
    public void CoreEngine_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreEngineAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Engine → Service", result));
    }

    /// <summary>Core.Application.Contracts は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreApplicationContracts_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreApplicationContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application.Contracts → Infrastructure", result));
    }

    /// <summary>Core.Application.Contracts は Service に依存しない。</summary>
    [Fact]
    public void CoreApplicationContracts_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreApplicationContractsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application.Contracts → Service", result));
    }

    /// <summary>Core.Application は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application → Infrastructure", result));
    }

    /// <summary>Core.Application は Service に依存しない。</summary>
    [Fact]
    public void CoreApplication_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Application → Service", result));
    }

    /// <summary>Core.Actions.Abstractions は Infrastructure に依存しない。</summary>
    [Fact]
    public void CoreActionsAbstractions_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(CoreActionsAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Actions.Abstractions → Infrastructure", result));
    }

    /// <summary>Core.Actions.Abstractions は Service に依存しない。</summary>
    [Fact]
    public void CoreActionsAbstractions_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(CoreActionsAbstractionsAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Core.Actions.Abstractions → Service", result));
    }

    private static Assembly InfraPersistenceAssembly =>
        typeof(Infrastructure.Persistence.DependencyInjection.PersistenceServiceCollectionExtensions).Assembly;

    private static Assembly InfraSecurityAssembly =>
        typeof(Infrastructure.Security.DependencyInjection.SecurityServiceCollectionExtensions).Assembly;

    private static Assembly InfraNotificationAssembly =>
        typeof(Infrastructure.Notification.DependencyInjection.NotificationServiceCollectionExtensions).Assembly;

    private static Assembly InfraModulesAssembly =>
        typeof(Infrastructure.Modules.DependencyInjection.ModulesServiceCollectionExtensions).Assembly;

    private static Assembly InfraCommonAssembly =>
        typeof(Infrastructure.Common.UuidV7Generator).Assembly;

    /// <summary>Infrastructure.Persistence は Service に依存しない。</summary>
    [Fact]
    public void InfraPersistence_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraPersistenceAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Persistence → Service", result));
    }

    /// <summary>Infrastructure.Security は Service に依存しない。</summary>
    [Fact]
    public void InfraSecurity_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraSecurityAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Security → Service", result));
    }

    /// <summary>Infrastructure.Notification は Service に依存しない。</summary>
    [Fact]
    public void InfraNotification_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraNotificationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Notification → Service", result));
    }

    /// <summary>Infrastructure.Modules は Service に依存しない。</summary>
    [Fact]
    public void InfraModules_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraModulesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Modules → Service", result));
    }

    /// <summary>Infrastructure.Common は Service に依存しない。</summary>
    [Fact]
    public void InfraCommon_ShouldNotDependOn_Service()
    {
        var result = Types.InAssembly(InfraCommonAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ServiceNamespaces)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure("Infrastructure.Common → Service", result));
    }

    private static string FormatFailure(string rule, TestResult result)
    {
        if (result.IsSuccessful) return string.Empty;

        var violators = result.FailingTypes?.Select(t => t.FullName ?? t.Name).ToList() ?? [];
        var sample = violators.Take(10).ToList();
        var message = $"依存方向違反: {rule}\n違反型({violators.Count}件):";
        foreach (var name in sample)
            message += $"\n  - {name}";
        if (violators.Count > 10)
            message += $"\n  ...他 {violators.Count - 10} 件";
        return message;
    }
}
