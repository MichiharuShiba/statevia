using System.Collections.Generic;
using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
using Statevia.Core.Engine;
using Statevia.Core.Execution;
using Xunit;

namespace Statevia.Core.Tests.Engine;

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
