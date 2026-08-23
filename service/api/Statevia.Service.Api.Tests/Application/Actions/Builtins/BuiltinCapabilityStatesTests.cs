using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary>sleep / signal / publish / workflow Builtin の単体テスト。</summary>
public sealed class BuiltinCapabilityStatesTests
{
    private sealed class FakeEventProvider : IEventProvider
    {
        public string? LastSignal { get; private set; }
        public string? LastTopic { get; private set; }

        public Task WaitAsync(string eventName, CancellationToken ct) => Task.CompletedTask;

        public Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct) =>
            Task.FromResult(eventNames[0]);

        public void Signal(string signalName) => LastSignal = signalName;

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

    private static StateContext MakeContext(IEventProvider events) =>
        new()
        {
            Events = events,
            Store = new FakeStore(),
            ExecutionId = Guid.NewGuid().ToString("D"),
            StateName = "S1",
        };

    /// <summary>sleep は duration 後に Unit を返す。</summary>
    [Fact]
    public async Task SleepActionState_WithDuration_ReturnsUnit()
    {
        // Arrange
        var state = new SleepActionState();
        var input = new Dictionary<string, object?> { ["duration"] = "20ms" };

        // Act
        var result = await state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result);
    }

    /// <summary>signal は指定シグナルを発行する。</summary>
    [Fact]
    public async Task SignalActionState_PublishesSignal()
    {
        // Arrange
        var events = new FakeEventProvider();
        var state = new SignalActionState();
        var input = new Dictionary<string, object?> { ["signal"] = "approval" };

        // Act
        await state.ExecuteAsync(MakeContext(events), input, CancellationToken.None);

        // Assert
        Assert.Equal("approval", events.LastSignal);
    }

    /// <summary>publish は topic dispatch を記録する。</summary>
    [Fact]
    public async Task PublishActionState_DispatchesTopic()
    {
        // Arrange
        var events = new FakeEventProvider();
        var state = new PublishActionState();
        var input = new Dictionary<string, object?> { ["topic"] = "payment.completed" };

        // Act
        var result = await state.ExecuteAsync(MakeContext(events), input, CancellationToken.None);

        // Assert
        Assert.Equal("payment.completed", events.LastTopic);
        Assert.IsType<Dictionary<string, object?>>(result);
    }

    /// <summary>signal は current 以外の target を拒否する。</summary>
    [Fact]
    public async Task SignalActionState_InvalidTarget_Throws()
    {
        // Arrange
        var state = new SignalActionState();
        var input = new Dictionary<string, object?>
        {
            ["signal"] = "approval",
            ["target"] = "other",
        };

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            state.ExecuteAsync(MakeContext(new FakeEventProvider()), input, CancellationToken.None));
    }
}
