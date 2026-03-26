using Statevia.Core.Api.Application.Actions.Builtins;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Api.Tests.Application.Actions.Builtins;

public sealed class BuiltinActionStatesTests
{
    private sealed class FakeEventProvider : IEventProvider
    {
        public string? LastEventName { get; private set; }

        public Task WaitAsync(string eventName, CancellationToken ct)
        {
            LastEventName = eventName;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStore : IReadOnlyStateStore
    {
        public bool TryGetOutput(string stateName, out object? output)
        {
            output = null;
            return false;
        }
    }

    private static StateContext MakeContext(IEventProvider events, IReadOnlyStateStore store) =>
        new StateContext
        {
            Events = events,
            Store = store,
            WorkflowId = Guid.NewGuid().ToString("D"),
            StateName = "S1"
        };

    /// <summary>
    /// 何もしない処理は空の値を返す。
    /// </summary>
    [Fact]
    public async Task NoOpState_ExecuteAsync_ReturnsUnit()
    {
        // Arrange
        var state = new NoOpState();
        var events = new FakeEventProvider();
        var store = new FakeStore();

        // Act
        var result = await state.ExecuteAsync(MakeContext(events, store), Unit.Value, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
        Assert.Null(events.LastEventName);
    }

    /// <summary>
    /// WaitOnlyStateは指定イベントを待機してUnit値を返す。
    /// </summary>
    [Fact]
    public async Task WaitOnlyState_ExecuteAsync_WaitsForEventAndReturnsUnit()
    {
        // Arrange
        var state = new WaitOnlyState("MyEvent");
        var events = new FakeEventProvider();
        var store = new FakeStore();

        // Act
        var result = await state.ExecuteAsync(MakeContext(events, store), Unit.Value, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
        Assert.Equal("MyEvent", events.LastEventName);
    }
}

