using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Collections.Generic;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

public class WorkflowEngineTests
{
    /// <summary>Start を呼ぶと空でないワークフロー ID が返ることを検証する。</summary>
    [Fact]
    public void Start_ReturnsWorkflowId()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });

        // Act
        var id = engine.Start(def);

        // Assert
        Assert.NotNull(id);
        Assert.NotEmpty(id);
    }

    /// <summary>存在しないワークフロー ID で GetSnapshot を呼ぶと null が返ることを検証する。</summary>
    [Fact]
    public void GetSnapshot_ReturnsNull_WhenWorkflowNotFound()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Act
        var snapshot = engine.GetSnapshot("non-existent");

        // Assert
        Assert.Null(snapshot);
    }

    /// <summary>存在するワークフロー ID で GetSnapshot を呼ぶと、正しいスナップショットが返ることを検証する。</summary>
    [Fact]
    public void GetSnapshot_ReturnsSnapshot_WhenWorkflowExists()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(id, snapshot.WorkflowId);
        Assert.Equal("Minimal", snapshot.WorkflowName);
        Assert.NotNull(snapshot.ActiveStates);
    }

    /// <summary>存在するワークフロー ID で ExportExecutionGraph を呼ぶと JSON が返ることを検証する。</summary>
    [Fact]
    public void ExportExecutionGraph_ReturnsJson_WhenWorkflowExists()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        var json = engine.ExportExecutionGraph(id);

        // Assert
        Assert.NotNull(json);
        Assert.NotEqual("{}", json);
    }

    /// <summary>存在しないワークフロー ID で ExportExecutionGraph を呼ぶと "{}" が返ることを検証する。</summary>
    [Fact]
    public void ExportExecutionGraph_ReturnsEmptyJson_WhenWorkflowNotFound()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Act
        var json = engine.ExportExecutionGraph("non-existent");

        // Assert
        Assert.Equal("{}", json);
    }

    /// <summary>end 状態に到達したワークフローが IsCompleted になることを検証する。</summary>
    [Fact]
    public async Task Start_WorkflowCompletes_WhenEndStateReached()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act: ワークフロー完了を待つ
        await Task.Delay(200);

        // Assert
        var snapshot = engine.GetSnapshot(id);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>PublishEvent を呼んでも例外が発生しないことを検証する。</summary>
    [Fact]
    public void PublishEvent_DoesNotThrow()
    {
        // Arrange
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        engine.Start(CreateMinimalDefinition());

        // Act
        engine.PublishEvent("SomeEvent");

        // Assert: 例外が発生しないこと
    }

    /// <summary>複数ワークフローが存在するとき、イベント名のみのブロードキャストが例外で終了しないこと。</summary>
    [Fact]
    public void PublishEvent_BroadcastByEventName_DoesNotThrow_WhenMultipleWorkflows()
    {
        // Arrange
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 2 });
        engine.Start(CreateMinimalDefinition());
        engine.Start(CreateMinimalDefinition());

        // Act
        engine.PublishEvent("SomeEvent");

        // Assert: 例外が発生しないこと
    }

    /// <summary>clientEventId 付き PublishEvent オーバーロードも従来と同様に例外を投げないことを検証する。</summary>
    [Fact]
    public void PublishEvent_WithClientEventId_DoesNotThrow()
    {
        // Arrange
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var workflowId = engine.Start(CreateMinimalDefinition());
        var clientEventId = Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");

        // Act
        var result = engine.PublishEvent(workflowId, "SomeEvent", clientEventId);

        // Assert
        Assert.True(result.IsApplied);
    }

    /// <summary>複数ワークフローが存在するとき、clientEventId 付きブロードキャストが全インスタンスに届き、2 回目は冪等になる。</summary>
    [Fact]
    public void PublishEvent_WithClientEventId_Broadcast_AppliesThenAlreadyApplied_ForMultipleWorkflows()
    {
        // Arrange
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 2 });
        engine.Start(CreateMinimalDefinition());
        engine.Start(CreateMinimalDefinition());
        var clientEventId = Guid.Parse("b2c3d4e5-f6a7-4890-b123-456789abcdef");

        // Act
        var firstBroadcast = engine.PublishEvent("SomeEvent", clientEventId);
        var secondBroadcast = engine.PublishEvent("SomeEvent", clientEventId);

        // Assert
        Assert.True(firstBroadcast.IsApplied);
        Assert.True(secondBroadcast.IsAlreadyApplied);
    }

    /// <summary>Dispose を呼んでも例外が発生しないことを検証する。</summary>
    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        engine.Start(CreateMinimalDefinition());

        // Act
        engine.Dispose();

        // Assert: 例外が発生しないこと
    }

    /// <summary>状態が例外を投げるとワークフローが IsFailed になることを検証する。</summary>
    [Fact]
    public async Task Start_WorkflowMarksFailed_WhenStateThrows()
    {
        // Arrange
        var def = CreateDefinitionWithFailingState();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(200);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsFailed);
    }

    /// <summary>通常ステートの完了時にノード完了通知ハンドラが呼び出されることを検証する。</summary>
    [Fact]
    public async Task SetNodeCompletedHandler_InvokesHandler_WhenNormalStateCompleted()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var callCount = 0;
        var called = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetNodeCompletedHandler(workflowId =>
        {
            Interlocked.Increment(ref callCount);
            called.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await called.Task.WaitAsync(timeout.Token);

        // Assert
        Assert.True(callCount >= 1);
    }

    /// <summary>Join 合成ノードの完了時にもノード完了通知ハンドラが呼び出されることを検証する。</summary>
    [Fact]
    public async Task SetNodeCompletedHandler_InvokesHandler_WhenJoinStateCompleted()
    {
        // Arrange
        var def = CreateDefinitionWithForkJoin();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 2 });
        var callCount = 0;
        var calledAtJoin = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetNodeCompletedHandler(workflowId =>
        {
            var current = Interlocked.Increment(ref callCount);
            if (current >= 4)
                calledAtJoin.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        var workflowId = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await calledAtJoin.Task.WaitAsync(timeout.Token);
        var snapshot = engine.GetSnapshot(workflowId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.True(callCount >= 4);
    }

    /// <summary>Fork 遷移で複数状態が並列実行され、Join 後に完了することを検証する。</summary>
    [Fact]
    public async Task Start_WorkflowCompletes_WithForkAndJoin()
    {
        // Arrange
        var def = CreateDefinitionWithForkJoin();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 2 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(500);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>条件遷移は order 昇順で評価され、最初に一致したケースが採用されることを検証する。</summary>
    [Fact]
    public async Task Start_ConditionalTransition_UsesOrderAndFirstMatchWins()
    {
        // Arrange
        string? selectedState = null;
        var def = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["score"] = 40 },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 20,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.score", Op = "gt", Value = 30 },
                    Target = new TransitionTarget { Next = "Manual" }
                },
                new CompiledTransitionCase
                {
                    Order = 10,
                    DeclarationIndex = 1,
                    When = new ConditionExpressionDefinition { Path = "$.score", Op = "exists" },
                    Target = new TransitionTarget { Next = "Auto" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: stateName => selectedState = stateName);
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Equal("Auto", selectedState);
    }

    /// <summary>条件不一致時に default 遷移へフォールバックすることを検証する。</summary>
    [Fact]
    public async Task Start_ConditionalTransition_UsesDefaultFallback()
    {
        // Arrange
        string? selectedState = null;
        var def = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["score"] = 5 },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 1,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.score", Op = "gt", Value = 10 },
                    Target = new TransitionTarget { Next = "Manual" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: stateName => selectedState = stateName);
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Equal("Fallback", selectedState);
    }

    /// <summary>条件遷移の診断が実行グラフ JSON の該当ノードに含まれることを検証する（T6 可観測性）。</summary>
    [Fact]
    public async Task Start_ConditionalTransition_ExportsRoutingDiagnosticsOnGraph()
    {
        // Arrange
        var def = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["score"] = 5 },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 1,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.score", Op = "gt", Value = 10 },
                    Target = new TransitionTarget { Next = "Manual" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: _ => { });
        using var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var graphJson = engine.ExportExecutionGraph(id);

        // Assert
        using var doc = JsonDocument.Parse(graphJson);
        var routeNode = doc.RootElement.GetProperty("nodes").EnumerateArray()
            .First(node =>
            {
                if (node.TryGetProperty("stateName", out var s1))
                {
                    return string.Equals(s1.GetString(), "Route", StringComparison.Ordinal);
                }

                return node.TryGetProperty("StateName", out var s2)
                    && string.Equals(s2.GetString(), "Route", StringComparison.Ordinal);
            });
        var routing = routeNode.TryGetProperty("conditionRouting", out var camelRouting)
            ? camelRouting
            : routeNode.GetProperty("ConditionRouting");
        Assert.Equal("Completed", routing.TryGetProperty("fact", out var f) ? f.GetString() : routing.GetProperty("Fact").GetString());
        var resolution = routing.TryGetProperty("resolution", out var res) ? res.GetString() : routing.GetProperty("Resolution").GetString();
        Assert.Equal("default_fallback", resolution);
        var matchedIdx = routing.TryGetProperty("matchedCaseIndex", out var mi) ? mi : routing.GetProperty("MatchedCaseIndex");
        Assert.Equal(JsonValueKind.Null, matchedIdx.ValueKind);
        var evalProp = routing.TryGetProperty("caseEvaluations", out var ce) ? ce : routing.GetProperty("CaseEvaluations");
        var evaluations = evalProp.EnumerateArray().ToList();
        Assert.Single(evaluations);
        var m0 = evaluations[0].TryGetProperty("matched", out var m) ? m : evaluations[0].GetProperty("Matched");
        Assert.False(m0.GetBoolean());
        var rc = evaluations[0].TryGetProperty("reasonCode", out var r) ? r : evaluations[0].GetProperty("ReasonCode");
        Assert.Equal("condition_false", rc.GetString());
    }

    /// <summary>in と between の条件演算子で遷移先を選択できることを検証する。</summary>
    [Fact]
    public async Task Start_ConditionalTransition_SupportsInAndBetween()
    {
        // Arrange
        string? selectedByIn = null;
        string? selectedByBetween = null;
        var inDefinition = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["band"] = 2 },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 1,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.band", Op = "in", Value = new[] { 1, 2, 3 } },
                    Target = new TransitionTarget { Next = "Auto" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: stateName => selectedByIn = stateName);
        var betweenDefinition = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["score"] = 15 },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 1,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.score", Op = "between", Value = new[] { 10, 20 } },
                    Target = new TransitionTarget { Next = "Manual" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: stateName => selectedByBetween = stateName);
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });

        // Act
        var inWorkflowId = engine.Start(inDefinition);
        var betweenWorkflowId = engine.Start(betweenDefinition);
        await Task.Delay(400);
        var inSnapshot = engine.GetSnapshot(inWorkflowId);
        var betweenSnapshot = engine.GetSnapshot(betweenWorkflowId);

        // Assert
        Assert.NotNull(inSnapshot);
        Assert.NotNull(betweenSnapshot);
        Assert.True(inSnapshot.IsCompleted);
        Assert.True(betweenSnapshot.IsCompleted);
        Assert.Equal("Auto", selectedByIn);
        Assert.Equal("Manual", selectedByBetween);
    }

    /// <summary>Store.TryGetOutput が状態実行中に利用される経路を検証する。</summary>
    [Fact]
    public async Task Start_StateCanReadOtherStateOutputViaStore()
    {
        // Arrange
        var def = CreateDefinitionWithStoreReader();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    /// <summary>状態に遷移が定義されていない場合、ProcessFact で HasTransition が false となり早期 return することを検証する。</summary>
    [Fact]
    public async Task Start_StateWithNoTransition_ProcessFactReturnsEarly()
    {
        // Arrange: Start は実行されるが遷移テーブルに Completed がない（空の遷移）
        var def = new CompiledWorkflowDefinition
        {
            Name = "NoTransition",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>> { ["Start"] = new Dictionary<string, TransitionTarget>() },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["Start"] = DefaultStateExecutor.Create(new ImmediateState()) })
        };
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(200);
        var snapshot = engine.GetSnapshot(id);

        // Assert: 完了にも失敗にもせず、遷移がないのでそのまま止まる
        Assert.NotNull(snapshot);
        Assert.False(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
    }

    /// <summary>状態実行中に StateContext の WorkflowId と StateName が参照される経路を検証する。</summary>
    [Fact]
    public async Task Start_StateCanReadWorkflowIdAndStateName()
    {
        // Arrange
        var def = new CompiledWorkflowDefinition
        {
            Name = "Ctx",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>> { ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } } },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "A",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["A"] = DefaultStateExecutor.Create(new ContextReaderState()) })
        };
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(200);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    private static CompiledWorkflowDefinition CreateDefinitionWithFailingState() => new()
    {
        Name = "Fail",
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
            ["Start"] = DefaultStateExecutor.Create(new ThrowingState())
        })
    };

    private static CompiledWorkflowDefinition CreateDefinitionWithForkJoin() => new()
    {
        Name = "ForkJoin",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } },
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "Join1" } },
            ["Join1"] = new Dictionary<string, TransitionTarget> { ["Joined"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>> { ["Start"] = new[] { "A", "B" } },
        JoinTable = new Dictionary<string, IReadOnlyList<string>> { ["Join1"] = new[] { "A", "B" } },
        WaitTable = new Dictionary<string, string>(),
        InitialState = "Start",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["A"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["B"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private static CompiledWorkflowDefinition CreateDefinitionWithStoreReader() => new()
    {
        Name = "StoreReader",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } },
            ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
        WaitTable = new Dictionary<string, string>(),
        InitialState = "A",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["A"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["B"] = DefaultStateExecutor.Create(new StoreReaderState())
        })
    };

    private static CompiledWorkflowDefinition CreateDefinitionWithConditionalRoute(
        object? routeOutput,
        IReadOnlyList<CompiledTransitionCase> cases,
        TransitionTarget defaultTarget,
        Action<string> onTerminalStateExecuted)
    {
        var orderedCases = cases
            .OrderBy(transitionCase => transitionCase.Order.HasValue ? 0 : 1)
            .ThenBy(transitionCase => transitionCase.Order ?? int.MaxValue)
            .ThenBy(transitionCase => transitionCase.DeclarationIndex)
            .ToList();

        return new CompiledWorkflowDefinition
        {
            Name = "ConditionalRoute",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Manual"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } },
                ["Auto"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } },
                ["Fallback"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ConditionalTransitions = new Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>
            {
                ["Route"] = new Dictionary<string, CompiledFactTransition>
                {
                    ["Completed"] = new CompiledFactTransition
                    {
                        Cases = orderedCases,
                        DefaultTarget = defaultTarget
                    }
                }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Route",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["Route"] = new DefaultStateExecutor((_, _, _) => Task.FromResult(routeOutput)),
                ["Manual"] = new DefaultStateExecutor((_, _, _) =>
                {
                    onTerminalStateExecuted("Manual");
                    return Task.FromResult<object?>(Unit.Value);
                }),
                ["Auto"] = new DefaultStateExecutor((_, _, _) =>
                {
                    onTerminalStateExecuted("Auto");
                    return Task.FromResult<object?>(Unit.Value);
                }),
                ["Fallback"] = new DefaultStateExecutor((_, _, _) =>
                {
                    onTerminalStateExecuted("Fallback");
                    return Task.FromResult<object?>(Unit.Value);
                })
            })
        };
    }

    private sealed class ThrowingState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => throw new InvalidOperationException("state failed");
    }

    private sealed class StoreReaderState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit input, CancellationToken ct)
        {
            if (ctx.Store.TryGetOutput("A", out var output))
            {
                _ = output; // 参照してカバレッジを通す（input は未使用のため破棄）
            }
            return Task.FromResult(Unit.Value);
        }
    }

    private sealed class ContextReaderState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct)
        {
            var w = ctx.WorkflowId;
            var s = ctx.StateName;
            return Task.FromResult(Unit.Value);
        }
    }

    private static CompiledWorkflowDefinition CreateMinimalDefinition() => new()
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
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }
}
