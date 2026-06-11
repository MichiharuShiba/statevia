using Microsoft.Extensions.DependencyInjection;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Execution;
using Statevia.Core.Api.Application.Actions.Catalog;
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
}
