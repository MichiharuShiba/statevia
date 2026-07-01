using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Application.Actions.Visibility;
using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary>Action 実行層テスト用 DI セットアップ。</summary>
internal static class ActionExecutionTestSupport
{
    internal static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    internal static IServiceProvider CreateProvider(
        IActionCatalog catalog,
        Guid? tenantId = null,
        string tenantKey = "test-tenant")
    {
        tenantId ??= DefaultTenantId;
        return CreateProviderCore(catalog, tenantId, tenantKey);
    }

    private static IServiceProvider CreateProviderCore(
        IActionCatalog catalog,
        Guid? tenantId,
        string tenantKey)
    {
        var services = new ServiceCollection();
        services.AddSingleton(catalog);
        services.AddSingleton<IActionVisibilityResolver, DefaultActionVisibilityResolver>();
        services.AddSingleton<IActionExecutionPolicy, AlwaysInProcessPolicy>();
        services.AddSingleton(Options.Create(new ExecutionPolicyOptions()));
        services.AddSingleton<IActionHostExecutionClient, UnconfiguredActionHostExecutionClient>();
        services.AddSingleton<IActionExecutionBackend, InProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, OutOfProcessBackend>();
        services.AddSingleton<IActionExecutionBackendSelector, ActionExecutionBackendSelector>();
        services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        services.AddSingleton<IActionExecutor, DispatchingActionExecutor>();

        var accessor = new TenantContextAccessor();
        if (tenantId is not null)
        {
            accessor.SetContext(new TenantContextState(
                tenantId.Value,
                tenantKey,
                PrincipalId: null,
                TenantLifecycle.Active));
        }

        services.AddSingleton<ITenantContextAccessor>(accessor);
        return services.BuildServiceProvider();
    }

    internal static IServiceProvider CreateProviderWithoutTenant(IActionCatalog catalog) =>
        CreateProviderCore(catalog, tenantId: null, tenantKey: "test-tenant");

    internal static IActionCatalog CreateCatalogWithBuiltins()
    {
        var catalog = new InMemoryActionCatalog();
        DefinitionCompilerService.RegisterBuiltinActions(catalog);
        return catalog;
    }

    internal sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>OutOfProcess 経路の単体テスト用スタブ。</summary>
    internal sealed class UnconfiguredActionHostExecutionClient : IActionHostExecutionClient
    {
        /// <inheritdoc />
        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ActionExecutionResult
            {
                Success = false,
                ErrorCode = "ActionHostNotConfigured",
                ErrorMessage = "Action Host is not configured for this test.",
            });
    }

    /// <summary>OutOfProcess 成功を返すテスト用スタブ。</summary>
    internal sealed class SuccessfulActionHostExecutionClient : IActionHostExecutionClient
    {
        /// <inheritdoc />
        public Task<ActionExecutionResult> ExecuteAsync(
            ActionExecutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ActionExecutionResult
            {
                Success = true,
                Output = request.Input,
            });
    }
}
