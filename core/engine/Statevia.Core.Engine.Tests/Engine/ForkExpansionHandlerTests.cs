using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Join;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>Hosted 向け Fork 展開ハンドラ差し替え。</summary>
public sealed class ForkExpansionHandlerTests
{
    /// <summary>Hosted ハンドラ登録時、分岐子が Join へ遷移すると子は終端する。</summary>
    [Fact]
    public async Task SetForkExpansionHandler_ChildTransitionToJoin_MarksCompleted()
    {
        // Arrange
        var def = CreateForkDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        engine.SetForkExpansionHandler(_ => Task.CompletedTask);

        // Act
        var executionId = engine.Start(def, initialState: "A");
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(executionId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
    }

    /// <summary>
    /// checkpoint に前回の startedJoins が残っていても CompletePhysicalJoin で再入場できる。
    /// </summary>
    [Fact]
    public async Task CompletePhysicalJoin_WhenStartedJoinsAlreadySet_ReentersAndCompletes()
    {
        // Arrange
        var def = CreateForkDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        var forked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(_ =>
        {
            forked.TrySetResult(true);
            return Task.CompletedTask;
        });

        var parentId = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await forked.Task.WaitAsync(timeout.Token);
        await Task.Delay(50);

        var exported = engine.ExportCheckpoint(parentId)
            ?? throw new InvalidOperationException("checkpoint missing");
        engine.Unload(parentId);

        // 循環 1 周目相当: Join 開始済みマークが残った断面を hydrate する。
        var checkpoint = new ExecutionRuntimeCheckpoint
        {
            SchemaVersion = exported.SchemaVersion,
            ExecutionId = exported.ExecutionId,
            DefinitionName = exported.DefinitionName,
            IsCompleted = exported.IsCompleted,
            IsCancelled = exported.IsCancelled,
            IsFailed = exported.IsFailed,
            ActiveStates = exported.ActiveStates,
            StateAttempts = exported.StateAttempts,
            StateOutputs = exported.StateOutputs,
            AppliedPublishClientEventIds = exported.AppliedPublishClientEventIds,
            AppliedCancelClientEventIds = exported.AppliedCancelClientEventIds,
            Context = exported.Context,
            Graph = exported.Graph,
            PendingWaits = exported.PendingWaits,
            Join = new CheckpointJoinData
            {
                StartedJoins = ["Join1"],
                JoinSourceNodeIds = exported.Join.JoinSourceNodeIds,
                JoinStateResults = new Dictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Join1"] = new Dictionary<string, CheckpointJoinObserved>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["A"] = new CheckpointJoinObserved { Fact = "Completed", Output = null },
                        ["B"] = new CheckpointJoinObserved { Fact = "Completed", Output = null }
                    }
                }
            }
        };
        engine.ImportCheckpoint(def, checkpoint);

        // Act
        engine.CompletePhysicalJoin(
            parentId,
            "Join1",
            new Dictionary<string, object?>
            {
                ["A"] = "round-2-a",
                ["B"] = "round-2-b"
            });
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(parentId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
    }

    /// <summary>ResetForPhysicalJoinReentry 後は同一 Join を再度 Begin できる。</summary>
    [Fact]
    public void JoinTracker_ResetForPhysicalJoinReentry_AllowsSecondBegin()
    {
        // Arrange
        var tracker = new JoinTracker(CreateForkDefinition());
        Assert.Null(tracker.RecordFact("A", "Completed", "a1"));
        Assert.Equal("Join1", tracker.RecordFact("B", "Completed", "b1"));
        Assert.True(tracker.TryBeginJoinExecution("Join1"));
        Assert.False(tracker.TryBeginJoinExecution("Join1"));

        // Act
        tracker.ResetForPhysicalJoinReentry("Join1");
        Assert.Null(tracker.RecordFact("A", "Completed", "a2"));
        Assert.Equal("Join1", tracker.RecordFact("B", "Completed", "b2"));

        // Assert
        Assert.True(tracker.TryBeginJoinExecution("Join1"));
        var inputs = tracker.GetJoinInputs("Join1");
        Assert.Equal("a2", inputs["A"]);
        Assert.Equal("b2", inputs["B"]);
    }

    /// <summary>CompletePhysicalJoin は候補 input で Join を充足し親を進める。</summary>
    [Fact]
    public async Task CompletePhysicalJoin_RunsJoinAndCompletesWhenEnd()
    {
        // Arrange
        var def = CreateForkDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        var unloaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(async evt =>
        {
            // Hosted: 親は Fork 後に分岐を走らせない（Unload 相当の待機）。
            await Task.Yield();
            unloaded.TrySetResult(true);
        });

        var parentId = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await unloaded.Task.WaitAsync(timeout.Token);
        await Task.Delay(50);

        // Act
        engine.CompletePhysicalJoin(
            parentId,
            "Join1",
            new Dictionary<string, object?>
            {
                ["A"] = "a-out",
                ["B"] = "b-out"
            });
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(parentId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>CompletePhysicalJoin の Context マージ後、親から $.states / $.vars を読める。</summary>
    [Fact]
    public async Task CompletePhysicalJoin_MergesChildContext_LastWins()
    {
        // Arrange
        var def = CreateForkDefinitionWithAfterJoin();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        var forked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(_ =>
        {
            forked.TrySetResult(true);
            return Task.CompletedTask;
        });

        var parentId = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await forked.Task.WaitAsync(timeout.Token);
        await Task.Delay(50);

        // Act
        engine.CompletePhysicalJoin(
            parentId,
            "Join1",
            new Dictionary<string, object?>
            {
                ["A"] = "a-out",
                ["B"] = "b-out"
            },
            [
                new PhysicalJoinContextFragment(
                    new Dictionary<string, object?>
                    {
                        ["A"] = new Dictionary<string, object?> { ["output"] = "first-a" }
                    },
                    new Dictionary<string, object?> { ["shared"] = "first" }),
                new PhysicalJoinContextFragment(
                    new Dictionary<string, object?>
                    {
                        ["A"] = new Dictionary<string, object?> { ["output"] = "second-a" },
                        ["B"] = new Dictionary<string, object?> { ["output"] = "b-val" }
                    },
                    new Dictionary<string, object?> { ["shared"] = "second" })
            ]);
        await Task.Delay(400);
        var checkpoint = engine.ExportCheckpoint(parentId);

        // Assert
        Assert.NotNull(checkpoint);
        Assert.True(checkpoint.IsCompleted);
        Assert.NotNull(checkpoint.Context.States);
        Assert.True(checkpoint.Context.States.ContainsKey("A"));
        Assert.Equal("second-a", checkpoint.Context.States["A"]!.Value.GetProperty("output").GetString());
        Assert.Equal("b-val", checkpoint.Context.States["B"]!.Value.GetProperty("output").GetString());
        Assert.Equal("second", checkpoint.Context.Vars!.Value.GetProperty("shared").GetString());
    }

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
        await notified.Task.WaitAsync(timeout.Token);
        await Task.Delay(100);
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
        await Task.Delay(500);
        var snapshot = engine.GetSnapshot(executionId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>
    /// ネスト: 内側 Join 完了後の次が親側 Join（ローカル未充足）のとき、物理子は終端する。
    /// </summary>
    [Fact]
    public async Task CompletePhysicalJoin_WhenNextIsUnsatisfiedOuterJoin_MarksChildCompleted()
    {
        // Arrange
        var def = CreateNestedForkMidChildDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        var forked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetForkExpansionHandler(_ =>
        {
            forked.TrySetResult(true);
            return Task.CompletedTask;
        });

        var childId = engine.Start(def, initialState: "InnerFork");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await forked.Task.WaitAsync(timeout.Token);
        await Task.Delay(50);

        // Act — 内側 Join のみ充足。外側 Join の依存（OuterFast / InnerFork）は子に無い。
        engine.CompletePhysicalJoin(
            childId,
            "InnerJoin",
            new Dictionary<string, object?>
            {
                ["InnerA"] = "a",
                ["InnerB"] = "b"
            });
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(childId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
        var graph = engine.ExportExecutionGraph(childId);
        Assert.Contains("InnerJoin", graph, StringComparison.Ordinal);
        Assert.DoesNotContain("\"stateName\":\"OuterJoin\"", graph, StringComparison.Ordinal);
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
        await completed.Task.WaitAsync(timeout.Token);
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

    private static CompiledWorkflowDefinition CreateForkDefinitionWithAfterJoin() => new()
    {
        Name = "ForkExpansionHandlerWithAfterJoin",
        InitialState = "Start",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = ["A", "B"] } },
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { Next = "After" } },
            ["After"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = ["A", "B"] },
        JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = ["A", "B"] },
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["A"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["B"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["After"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private static CompiledWorkflowDefinition CreateNestedForkMidChildDefinition() => new()
    {
        Name = "NestedForkMidChild",
        InitialState = "OuterFork",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["OuterFork"] = new Dictionary<string, TransitionTarget>
            {
                ["Completed"] = new TransitionTarget { Fork = ["OuterFast", "InnerFork"] }
            },
            ["OuterFast"] = new Dictionary<string, TransitionTarget>
            {
                ["Completed"] = new TransitionTarget { Next = "OuterJoin" }
            },
            ["InnerFork"] = new Dictionary<string, TransitionTarget>
            {
                ["Completed"] = new TransitionTarget { Fork = ["InnerA", "InnerB"] }
            },
            ["InnerA"] = new Dictionary<string, TransitionTarget>
            {
                ["Completed"] = new TransitionTarget { Next = "InnerJoin" }
            },
            ["InnerB"] = new Dictionary<string, TransitionTarget>
            {
                ["Completed"] = new TransitionTarget { Next = "InnerJoin" }
            },
            ["InnerJoin"] = new Dictionary<string, TransitionTarget>
            {
                ["Joined"] = new TransitionTarget { Next = "OuterJoin" }
            },
            ["OuterJoin"] = new Dictionary<string, TransitionTarget>
            {
                ["Joined"] = new TransitionTarget { End = true }
            }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>
        {
            ["OuterFork"] = ["OuterFast", "InnerFork"],
            ["InnerFork"] = ["InnerA", "InnerB"]
        },
        JoinTable = new Dictionary<string, IReadOnlyList<string>>
        {
            ["InnerJoin"] = ["InnerA", "InnerB"],
            ["OuterJoin"] = ["OuterFast", "InnerFork"]
        },
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(
            StringComparer.OrdinalIgnoreCase),
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["OuterFork"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["OuterFast"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["InnerFork"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["InnerA"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["InnerB"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit input, CancellationToken ct) =>
            Task.FromResult(Unit.Value);
    }
}
