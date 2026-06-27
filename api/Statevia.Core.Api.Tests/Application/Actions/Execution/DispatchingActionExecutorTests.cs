using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Actions.Abstractions.Visibility;
using Statevia.Core.Api.Application.Actions.Catalog;
using Statevia.Core.Api.Application.Actions.Execution;
using Statevia.Core.Api.Application.Actions.Visibility;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="DispatchingActionExecutor"/> の単体テスト。</summary>
public sealed class DispatchingActionExecutorTests
{
    private sealed class EchoState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(input);
    }

    /// <summary>常に指定 Mode を返す Policy スタブ。</summary>
    private sealed class FixedModePolicy(ActionExecutionMode mode) : IActionExecutionPolicy
    {
        public ActionExecutionMode Resolve(ActionExecutionContext context, ActionDescriptor descriptor) => mode;
    }

    /// <summary>Builtin action を DispatchingActionExecutor 経由で実行できる。</summary>
    [Fact]
    public async Task ExecuteAsync_BuiltinNoOp_Succeeds()
    {
        // Arrange
        var catalog = ActionExecutionTestSupport.CreateCatalogWithBuiltins();
        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        var sut = provider.GetRequiredService<IActionExecutor>();
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };

        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = "statevia.action.builtin.noop",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

        // Act
        var result = await sut.ExecuteAsync(request, ctx, runtimeInput: null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
    }

    /// <summary>未登録 Action は UnknownAction を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_UnknownAction_ReturnsFailure()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        var sut = provider.GetRequiredService<IActionExecutor>();
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = "missing.action",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

        // Act
        var result = await sut.ExecuteAsync(request, ctx, runtimeInput: null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("UnknownAction", result.ErrorCode);
    }

    /// <summary>他テナント Action は実行時に拒否する。</summary>
    [Fact]
    public async Task ExecuteAsync_OtherTenantAction_ReturnsNotVisible()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = "tenant.only.action",
                ModuleId = "test.module",
                Version = "1.0.0",
                Visibility = ActionVisibility.Tenant,
                OwnerTenantId = "22222222-2222-2222-2222-222222222222",
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new EchoState())));

        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        var sut = provider.GetRequiredService<IActionExecutor>();
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = "tenant.only.action",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

        // Act
        var result = await sut.ExecuteAsync(request, ctx, runtimeInput: 1, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("ActionNotVisible", result.ErrorCode);
    }

    /// <summary>Policy が OutOfProcess を返す Action は Action Host 経由で実行する。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenPolicyRequiresOutOfProcess_UsesActionHost()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        catalog.Register(
            new ActionDescriptor
            {
                ActionId = "community.action",
                ModuleId = "test.module",
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Community,
                Source = ActionSourceKind.Filesystem,
                Visibility = ActionVisibility.Tenant,
                OwnerTenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new EchoState())));

        var services = new ServiceCollection();
        services.AddSingleton(catalog);
        services.AddSingleton<IActionCatalog>(catalog);
        services.AddSingleton<IActionVisibilityResolver, DefaultActionVisibilityResolver>();
        services.AddSingleton<IActionExecutionPolicy, ConfigurableExecutionPolicy>();
        services.AddSingleton(Options.Create(new ExecutionPolicyOptions()));
        services.AddSingleton<IActionHostExecutionClient, ActionExecutionTestSupport.SuccessfulActionHostExecutionClient>();
        services.AddSingleton<IActionExecutionBackend, InProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, OutOfProcessBackend>();
        services.AddSingleton<IActionExecutionBackendSelector, ActionExecutionBackendSelector>();
        services.AddSingleton<IHostEnvironment>(new ActionExecutionTestSupport.TestHostEnvironment
        {
            EnvironmentName = Environments.Production,
        });
        services.AddSingleton<IActionExecutor, DispatchingActionExecutor>();
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IActionExecutor>();
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = "community.action",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
            Input = System.Text.Json.JsonDocument.Parse("""{"ok":true}""").RootElement.Clone(),
        };

        // Act
        var result = await sut.ExecuteAsync(request, ctx, runtimeInput: null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
    }

    /// <summary>Backend 未登録の Mode を Policy が返した場合は UnsupportedExecutionMode を返す。</summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoBackendForResolvedMode_ReturnsUnsupported()
    {
        // Arrange
        var catalog = ActionExecutionTestSupport.CreateCatalogWithBuiltins();
        var services = new ServiceCollection();
        services.AddSingleton(catalog);
        services.AddSingleton(catalog);
        services.AddSingleton<IActionVisibilityResolver, DefaultActionVisibilityResolver>();
        services.AddSingleton<IActionExecutionPolicy>(new FixedModePolicy(ActionExecutionMode.Container));
        services.AddSingleton(Options.Create(new ExecutionPolicyOptions()));
        services.AddSingleton<IActionHostExecutionClient, ActionExecutionTestSupport.UnconfiguredActionHostExecutionClient>();
        services.AddSingleton<IActionExecutionBackend, InProcessBackend>();
        services.AddSingleton<IActionExecutionBackend, OutOfProcessBackend>();
        services.AddSingleton<IActionExecutionBackendSelector, ActionExecutionBackendSelector>();
        services.AddSingleton<IHostEnvironment, ActionExecutionTestSupport.TestHostEnvironment>();
        services.AddSingleton<IActionExecutor, DispatchingActionExecutor>();
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IActionExecutor>();
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };
        var request = new ActionExecutionRequest
        {
            ExecutionId = "exec-1",
            StateName = "A",
            ActionId = "statevia.action.builtin.noop",
            TenantId = ActionExecutionTestSupport.DefaultTenantId.ToString("D"),
        };

        // Act
        var result = await sut.ExecuteAsync(request, ctx, runtimeInput: null, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("UnsupportedExecutionMode", result.ErrorCode);
    }
}
