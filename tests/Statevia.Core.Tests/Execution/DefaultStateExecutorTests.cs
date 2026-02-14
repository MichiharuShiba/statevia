using Statevia.Core.Abstractions;
using Statevia.Core.Execution;
using Xunit;

namespace Statevia.Core.Tests.Execution;

public class DefaultStateExecutorTests
{
    /// <summary>DefaultStateExecutor が状態の出力を返すことを検証する。</summary>
    [Fact]
    public async Task ExecuteAsync_ReturnsStateOutput()
    {
        // Arrange
        var state = new TestState();
        var executor = DefaultStateExecutor.Create(state);
        var ctx = CreateContext();

        // Act
        var result = await executor.ExecuteAsync(ctx, Unit.Value, CancellationToken.None);

        // Assert
        Assert.Equal("done", result);
    }

    /// <summary>DefaultStateExecutor が入力を状態に渡し、そのまま出力として返すことを検証する。</summary>
    [Fact]
    public async Task ExecuteAsync_PassesInputToState()
    {
        // Arrange
        var state = new EchoState();
        var executor = DefaultStateExecutor.Create(state);
        var ctx = CreateContext();

        // Act
        var result = await executor.ExecuteAsync(ctx, 42, CancellationToken.None);

        // Assert
        Assert.Equal(42, result);
    }

    private static StateContext CreateContext() => new()
    {
        Events = new NullEventProvider(),
        Store = new NullStateStore(),
        WorkflowId = "test",
        StateName = "Test"
    };

    private sealed class TestState : IState<Unit, string>
    {
        public Task<string> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult("done");
    }

    private sealed class EchoState : IState<int, int>
    {
        public Task<int> ExecuteAsync(StateContext ctx, int input, CancellationToken ct) => Task.FromResult(input);
    }

    private sealed class NullEventProvider : IEventProvider
    {
        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NullStateStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output) { output = null; return false; }
    }
}
