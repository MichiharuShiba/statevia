using System;
using System.Collections.Generic;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public class CancelTests
{
    /// <summary>協調的キャンセルを呼ぶと、ワークフローインスタンスが IsCancelled で停止することを検証する。</summary>
    [Fact]
    public async Task CancelAsync_MarksInstanceCancelled()
    {
        // Arrange: 長時間実行する状態を含む定義とエンジンを準備
        var def = CreateDefinitionWithLongRunningState();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);
        await Task.Delay(50);

        // Act: キャンセルを実行
        await engine.CancelAsync(id);

        // Assert: スナップショットが取得でき、IsCancelled が true であること
        var snapshot = engine.GetSnapshot(id);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCancelled);
    }

    /// <summary>clientEventId 付き CancelAsync オーバーロードは二引数版と同様にインスタンスをキャンセルする。</summary>
    [Fact]
    public async Task CancelAsync_WithClientEventId_MarksInstanceCancelled_SameAsTwoArg()
    {
        // Arrange
        var def = CreateDefinitionWithLongRunningState();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);
        await Task.Delay(50);
        var clientEventId = Guid.Parse("b2c3d4e5-f6a7-4890-b123-456789abcdef");

        // Act
        var first = await engine.CancelAsync(id, clientEventId);
        var second = await engine.CancelAsync(id, clientEventId);

        // Assert
        Assert.True(first.IsApplied);
        Assert.True(second.IsAlreadyApplied);
        var snapshot = engine.GetSnapshot(id);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCancelled);
    }

    private static CompiledWorkflowDefinition CreateDefinitionWithLongRunningState() => new()
    {
        Name = "Minimal",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
        WaitTable = new Dictionary<string, string>(),
        InitialState = "Start",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new LongRunningState())
        })
    };

    /// <summary>キャンセルせずに待つと長時間状態が完了し IsCompleted になることを検証する。</summary>
    [Fact]
    public async Task Start_Completes_WhenLongRunningStateFinishes()
    {
        // Arrange
        var def = CreateDefinitionWithLongRunningState();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(5100);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    private sealed class LongRunningState : IState<Unit, Unit>
    {
        public async Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
        {
            await Task.Delay(5000, ct);
            return Unit.Value;
        }
    }
}
