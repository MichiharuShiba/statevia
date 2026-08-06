using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>Hosted 向け Fork 展開ハンドラ差し替え。</summary>
public sealed class ForkExpansionHandlerTests
{
    /// <summary>ハンドラ登録時は論理 Fork せず、写像済み分岐を通知する。</summary>
    [Fact]
    public async Task SetForkExpansionHandler_DoesNotScheduleBranches_AndNotifiesPlans()
    {
        // Arrange
        var def = CreateForkDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        ForkExpansionEvent? captured = null;
        var notified = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(evt =>
        {
            captured = evt;
            notified.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        var executionId = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await notified.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);
        var snapshot = engine.GetSnapshot(executionId);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(executionId, captured.ExecutionId);
        Assert.Equal("Start", captured.ForkState);
        Assert.Equal(2, captured.Branches.Count);
        Assert.Contains(captured.Branches, b => b.BranchState == "A");
        Assert.Contains(captured.Branches, b => b.BranchState == "B");
        Assert.NotNull(snapshot);
        Assert.False(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
    }

    /// <summary>ハンドラ未登録時は従来どおり Fork/Join で完了する。</summary>
    [Fact]
    public async Task WithoutForkExpansionHandler_LogicalForkStillCompletes()
    {
        // Arrange
        var def = CreateForkDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);

        // Act
        var executionId = engine.Start(def);
        await Task.Delay(500).ConfigureAwait(false);
        var snapshot = engine.GetSnapshot(executionId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>initialState 指定時はその状態から開始する。</summary>
    [Fact]
    public async Task Start_WithInitialStateOverride_StartsAtBranch()
    {
        // Arrange
        var def = CreateForkDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetNodeCompletedHandler(_ =>
        {
            completed.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        var executionId = engine.Start(def, initialState: "A");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await completed.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        var graph = engine.ExportExecutionGraph(executionId);

        // Assert
        Assert.Contains("A", graph, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Start\"", graph, StringComparison.Ordinal);
    }

    private static CompiledWorkflowDefinition CreateForkDefinition() => new()
    {
        Name = "ForkExpansionHandlerSample",
        InitialState = "Start",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = ["A", "B"] } },
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = ["A", "B"] },
        JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = ["A", "B"] },
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["A"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["B"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit input, CancellationToken ct) =>
            Task.FromResult(Unit.Value);
    }
}
