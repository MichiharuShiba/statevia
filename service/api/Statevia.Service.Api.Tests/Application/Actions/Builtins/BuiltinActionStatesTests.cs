using Statevia.Core.Engine.Abstractions;
using Statevia.Service.Api.Application.Actions.Builtins;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

public sealed class BuiltinActionStatesTests
{
    private sealed class FakeEventProvider : IEventProvider
    {
        public string? LastEventName { get; private set; }
        public string? LastSignalName { get; private set; }
        public string? LastTopic { get; private set; }

        public Task WaitAsync(string eventName, CancellationToken ct)
        {
            LastEventName = eventName;
            return Task.CompletedTask;
        }

        public string? LastNodeId { get; private set; }

        public IReadOnlyList<string>? LastEventNames { get; private set; }

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct)
        {
            LastNodeId = nodeId;
            LastEventNames = eventNames;
            LastEventName = eventNames[0];
            return Task.FromResult(eventNames[0]);
        }

        public void Signal(string signalName) => LastSignalName = signalName;

        public void Resume(string nodeId, string eventName) { }

        public void PublishTopic(string topic, object? payloadSummary) => LastTopic = topic;
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
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "S1"
        };

    /// <summary>
    /// noop は副作用を起こさず、入力を出力としてそのまま返す。
    /// </summary>
    [Fact]
    public async Task NoOpState_ExecuteAsync_ReturnsInputUnchanged()
    {
        // Arrange
        var state = new NoOpState();
        var events = new FakeEventProvider();
        var store = new FakeStore();
        var payload = new Dictionary<string, object?> { ["eligible"] = true };

        // Act
        var result = await state.ExecuteAsync(MakeContext(events, store), payload, CancellationToken.None);

        // Assert
        Assert.Same(payload, result);
        Assert.Null(events.LastEventName);
    }

    /// <summary>
    /// noop は null 入力でも null を返す。
    /// </summary>
    [Fact]
    public async Task NoOpState_ExecuteAsync_WhenInputNull_ReturnsNull()
    {
        // Arrange
        var state = new NoOpState();
        var events = new FakeEventProvider();
        var store = new FakeStore();

        // Act
        var result = await state.ExecuteAsync(MakeContext(events, store), null, CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Null(events.LastEventName);
    }

    /// <summary>
    /// WaitOnlyState はノードスコープでイベントを待機し、監査用 output.event を返す。
    /// </summary>
    [Fact]
    public async Task WaitOnlyState_ExecuteAsync_WaitsForEventAndReturnsEventOutput()
    {
        // Arrange
        var state = new WaitOnlyState(["approve", "reject"]);
        var events = new FakeEventProvider();
        var store = new FakeStore();
        var ctx = new StateContext
        {
            Events = events,
            Store = store,
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "ApproveTask",
            NodeId = "node-approve-1",
        };

        // Act
        var result = await state.ExecuteAsync(ctx, Unit.Value, CancellationToken.None);

        // Assert
        Assert.Equal("approve", result["event"]);
        Assert.Equal("node-approve-1", events.LastNodeId);
        Assert.Equal(new[] { "approve", "reject" }, events.LastEventNames);
    }

    /// <summary>NodeId 未設定時は StateName を待機キーに使う。</summary>
    [Fact]
    public async Task WaitOnlyState_ExecuteAsync_FallsBackToStateName_WhenNodeIdMissing()
    {
        // Arrange
        var state = new WaitOnlyState(["MyEvent"]);
        var events = new FakeEventProvider();
        var store = new FakeStore();

        // Act
        var result = await state.ExecuteAsync(MakeContext(events, store), Unit.Value, CancellationToken.None);

        // Assert
        Assert.Equal("MyEvent", result["event"]);
        Assert.Equal("S1", events.LastNodeId);
    }

    /// <summary>
    /// DelayCompleteState は指定時間後に Unit を返す。
    /// </summary>
    [Fact]
    public async Task DelayCompleteState_ExecuteAsync_AfterDelay_ReturnsUnit()
    {
        // Arrange
        var state = new DelayCompleteState(TimeSpan.FromMilliseconds(20));
        var events = new FakeEventProvider();
        var store = new FakeStore();

        // Act
        var result = await state.ExecuteAsync(MakeContext(events, store), Unit.Value, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
        Assert.Null(events.LastEventName);
    }

    /// <summary>
    /// DelayCompleteState はキャンセル時に完了前であれば操作キャンセル例外で打ち切られる。
    /// </summary>
    [Fact]
    public async Task DelayCompleteState_ExecuteAsync_WhenCanceledBeforeDelay_Throws()
    {
        // Arrange
        var state = new DelayCompleteState(TimeSpan.FromHours(1));
        var events = new FakeEventProvider();
        var store = new FakeStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => state.ExecuteAsync(MakeContext(events, store), Unit.Value, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }
}

