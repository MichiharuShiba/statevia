using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Actions.Abstractions.Visibility;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Application.Actions.Catalog;
using Statevia.Core.Api.Application.Actions.Execution;
using Statevia.Core.Api.Application.Actions.Visibility;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure.Security;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

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
        services.AddSingleton<InProcessBackend>();
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Statevia.Core.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
