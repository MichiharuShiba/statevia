using Microsoft.Extensions.DependencyInjection;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Core.Api.Application.Actions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="InProcessBackend"/> の単体テスト。</summary>
public sealed class InProcessBackendTests
{
    private sealed class EchoState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(input);
    }

    private static ActionRegistration CreateRegistration(bool withFactory = true)
    {
        var descriptor = new ActionDescriptor
        {
            ActionId = "test.action",
            ModuleId = "test.module",
            Version = "1.0.0",
            Visibility = ActionVisibility.Builtin,
        };
        var entry = withFactory
            ? new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new EchoState()))
            : new ActionCatalogEntry(InProcessFactory: null!);
        return new ActionRegistration(descriptor, entry);
    }

    /// <summary>InProcessFactory 経由で状態実行器を呼び出す。</summary>
    [Fact]
    public async Task ExecuteAsync_WithFactory_ReturnsExecutorOutput()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };

        // Act
        var output = await sut.ExecuteAsync(
            CreateRegistration(),
            ctx,
            runtimeInput: 7,
            CancellationToken.None);

        // Assert
        Assert.Equal(7, output);
    }

    /// <summary>InProcessFactory 未設定は InvalidOperationException。</summary>
    [Fact]
    public async Task ExecuteAsync_WithoutFactory_Throws()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new InProcessBackend(provider);
        var ctx = new StateContext
        {
            Events = null!,
            Store = null!,
            ExecutionId = "exec-1",
            StateName = "A",
        };

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(
                CreateRegistration(withFactory: false),
                ctx,
                runtimeInput: null,
                CancellationToken.None));
    }
}
