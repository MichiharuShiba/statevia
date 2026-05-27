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

public class ExecutionEngineTests
{
    /// <summary>Start を呼ぶと空でない execution ID が返ることを検証する。</summary>
    [Fact]
    public void Start_ReturnsExecutionId()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);

        // Assert
        Assert.NotNull(id);
        Assert.NotEmpty(id);
    }

    /// <summary>存在しない execution ID で GetSnapshot を呼ぶと null が返ることを検証する。</summary>
    [Fact]
    public void GetSnapshot_ReturnsNull_WhenExecutionNotFound()
    {
        // Arrange
        var engine = ExecutionEngineTestHarness.Create();

        // Act
        var snapshot = engine.GetSnapshot("non-existent");

        // Assert
        Assert.Null(snapshot);
    }

    /// <summary>存在する execution ID で GetSnapshot を呼ぶと、正しいスナップショットが返ることを検証する。</summary>
    [Fact]
    public void GetSnapshot_ReturnsSnapshot_WhenExecutionExists()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(id, snapshot.ExecutionId);
        Assert.Equal("Minimal", snapshot.WorkflowName);
        Assert.NotNull(snapshot.ActiveStates);
    }

    /// <summary>存在する execution ID で ExportExecutionGraph を呼ぶと JSON が返ることを検証する。</summary>
    [Fact]
    public void ExportExecutionGraph_ReturnsJson_WhenExecutionExists()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        var json = engine.ExportExecutionGraph(id);

        // Assert
        Assert.NotNull(json);
        Assert.NotEqual("{}", json);
    }

    /// <summary>存在しない execution ID で ExportExecutionGraph を呼ぶと "{}" が返ることを検証する。</summary>
    [Fact]
    public void ExportExecutionGraph_ReturnsEmptyJson_WhenExecutionNotFound()
    {
        // Arrange
        var engine = ExecutionEngineTestHarness.Create();

        // Act
        var json = engine.ExportExecutionGraph("non-existent");

        // Assert
        Assert.Equal("{}", json);
    }

    /// <summary>end 状態に到達した実行が IsCompleted になることを検証する。</summary>
    [Fact]
    public async Task Start_ExecutionCompletes_WhenEndStateReached()
    {
        // Arrange
        var def = CreateMinimalDefinition();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act: 実行完了を待つ
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        engine.Start(CreateMinimalDefinition());

        // Act
        var ex = Record.Exception(() => engine.PublishEvent("SomeEvent"));

        // Assert
        Assert.Null(ex);
    }

    /// <summary>単一 execution ID 指定の PublishEvent が例外を投げないことを検証する。</summary>
    [Fact]
    public void PublishEvent_ToExecutionId_DoesNotThrow()
    {
        // Arrange
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateMinimalDefinition());

        // Act
        var ex = Record.Exception(() => engine.PublishEvent(executionId, "SomeEvent"));

        // Assert
        Assert.Null(ex);
    }

    /// <summary>複数実行が存在するとき、イベント名のみのブロードキャストが例外で終了しないこと。</summary>
    [Fact]
    public void PublishEvent_BroadcastByEventName_DoesNotThrow_WhenMultipleExecutions()
    {
        // Arrange
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        engine.Start(CreateMinimalDefinition());
        engine.Start(CreateMinimalDefinition());

        // Act
        var ex = Record.Exception(() => engine.PublishEvent("SomeEvent"));

        // Assert
        Assert.Null(ex);
    }

    /// <summary>clientEventId 付き PublishEvent オーバーロードも従来と同様に例外を投げないことを検証する。</summary>
    [Fact]
    public void PublishEvent_WithClientEventId_DoesNotThrow()
    {
        // Arrange
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateMinimalDefinition());
        var clientEventId = Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");

        // Act
        var result = engine.PublishEvent(executionId, "SomeEvent", clientEventId);

        // Assert
        Assert.True(result.IsApplied);
    }

    /// <summary>複数実行が存在するとき、clientEventId 付きブロードキャストが全インスタンスに届き、2 回目は冪等になる。</summary>
    [Fact]
    public void PublishEvent_WithClientEventId_Broadcast_AppliesThenAlreadyApplied_ForMultipleExecutions()
    {
        // Arrange
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        engine.Start(CreateMinimalDefinition());

        // Act
        var ex = Record.Exception(() => engine.Dispose());

        // Assert
        Assert.Null(ex);
    }

    /// <summary>状態が例外を投げると実行が IsFailed になることを検証する。</summary>
    [Fact]
    public async Task Start_ExecutionMarksFailed_WhenStateThrows()
    {
        // Arrange
        var def = CreateDefinitionWithFailingState();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var callCount = 0;
        var called = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetNodeCompletedHandler(executionId =>
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        var callCount = 0;
        var calledAtJoin = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SetNodeCompletedHandler(executionId =>
        {
            var current = Interlocked.Increment(ref callCount);
            if (current >= 4)
                calledAtJoin.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        var executionId = engine.Start(def);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await calledAtJoin.Task.WaitAsync(timeout.Token);
        var snapshot = engine.GetSnapshot(executionId);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.True(callCount >= 4);
    }

    /// <summary>Fork 遷移で複数状態が並列実行され、Join 後に完了することを検証する。</summary>
    [Fact]
    public async Task Start_ExecutionCompletes_WithForkAndJoin()
    {
        // Arrange
        var def = CreateDefinitionWithForkJoin();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 2);
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

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
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var graphJson = engine.ExportExecutionGraph(id);

        // Assert
        using var doc = JsonDocument.Parse(graphJson);
        var routeNode = doc.RootElement.GetProperty("nodes").EnumerateArray()
            .First(node =>
            {
                return node.TryGetProperty("stateName", out var s1)
                    && string.Equals(s1.GetString(), "Route", StringComparison.Ordinal);
            });
        Assert.False(routeNode.TryGetProperty("ConditionRouting", out _));
        var routing = routeNode.GetProperty("conditionRouting");
        Assert.Equal("Completed", routing.GetProperty("fact").GetString());
        Assert.Equal(ConditionRoutingResolutions.DefaultFallback, routing.GetProperty("resolution").GetString());
        var matchedIdx = routing.GetProperty("matchedCaseIndex");
        Assert.Equal(JsonValueKind.Null, matchedIdx.ValueKind);
        var evaluations = routing.GetProperty("caseEvaluations").EnumerateArray().ToList();
        Assert.Single(evaluations);
        Assert.False(evaluations[0].GetProperty("matched").GetBoolean());
        Assert.Equal("condition_false", evaluations[0].GetProperty("reasonCode").GetString());
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var inExecutionId = engine.Start(inDefinition);
        var betweenExecutionId = engine.Start(betweenDefinition);
        await Task.Delay(400);
        var inSnapshot = engine.GetSnapshot(inExecutionId);
        var betweenSnapshot = engine.GetSnapshot(betweenExecutionId);

        // Assert
        Assert.NotNull(inSnapshot);
        Assert.NotNull(betweenSnapshot);
        Assert.True(inSnapshot.IsCompleted);
        Assert.True(betweenSnapshot.IsCompleted);
        Assert.Equal("Auto", selectedByIn);
        Assert.Equal("Manual", selectedByBetween);
    }

    /// <summary>
    /// <c>eq</c> で定義値が真偽のとき、実値が 0/1 の数値でも一致扱いになることを検証する。
    /// </summary>
    [Fact]
    public async Task Start_ConditionalTransition_Eq_TrueMatchesWhenActualIsIntegralOne()
    {
        // Arrange
        string? selectedState = null;
        var def = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["eligible"] = 1L },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 10,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.eligible", Op = "eq", Value = true },
                    Target = new TransitionTarget { Next = "Manual" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: stateName => selectedState = stateName);
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Equal("Manual", selectedState);
    }

    /// <summary>
    /// <c>eq</c> で定義値が文字列 <c>true</c> のとき、実値が真偽でも一致することを検証する。
    /// </summary>
    [Fact]
    public async Task Start_ConditionalTransition_Eq_StringTrueMatchesActualBoolean()
    {
        // Arrange
        string? selectedState = null;
        var def = CreateDefinitionWithConditionalRoute(
            routeOutput: new Dictionary<string, object?> { ["eligible"] = true },
            cases:
            [
                new CompiledTransitionCase
                {
                    Order = 10,
                    DeclarationIndex = 0,
                    When = new ConditionExpressionDefinition { Path = "$.eligible", Op = "eq", Value = "true" },
                    Target = new TransitionTarget { Next = "Manual" }
                }
            ],
            defaultTarget: new TransitionTarget { Next = "Fallback" },
            onTerminalStateExecuted: stateName => selectedState = stateName);
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Equal("Manual", selectedState);
    }

    /// <summary>
    /// Start の <c>input</c> が初期状態に渡り、その状態の出力が条件式（OutputConditionEvaluator）で評価されて遷移先が決まることを検証する。
    /// </summary>
    [Fact]
    public async Task Start_Input_maps_to_route_output_then_conditional_selects_manual()
    {
        // Arrange
        object? observedRouteInput = null;
        string? selectedState = null;
        var input = new Dictionary<string, object?> { ["score"] = 42 };
        var def = CreateDefinitionWithConditionalRouteFromStartInput(
            routeOutputFromInput: input =>
            {
                observedRouteInput = input;
                return new Dictionary<string, object?> { ["score"] = ReadScoreFromStartInput(input) };
            },
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
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def, null, input);
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Same(input, observedRouteInput);
        Assert.Equal("Manual", selectedState);
    }

    /// <summary>
    /// input 由来の出力が条件に一致しないとき default 遷移へ進むことを検証する。
    /// </summary>
    [Fact]
    public async Task Start_Input_maps_to_route_output_then_conditional_selects_default_fallback()
    {
        // Arrange
        object? observedRouteInput = null;
        string? selectedState = null;
        var input = new Dictionary<string, object?> { ["score"] = 5 };
        var def = CreateDefinitionWithConditionalRouteFromStartInput(
            routeOutputFromInput: input =>
            {
                observedRouteInput = input;
                return new Dictionary<string, object?> { ["score"] = ReadScoreFromStartInput(input) };
            },
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
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def, null, input);
        await Task.Delay(300);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
        Assert.Same(input, observedRouteInput);
        Assert.Equal("Fallback", selectedState);
    }

    /// <summary>同一 stateName が再訪されたとき execution graph の attempt が増加することを検証する。</summary>
    [Fact]
    public async Task Start_WhenStateNameRevisited_AttemptIncrementsPerStateName()
    {
        // Arrange
        var def = CreateDefinitionWithStateRevisit();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await Task.Delay(400);
        var graphJson = engine.ExportExecutionGraph(id);

        // Assert
        using var doc = JsonDocument.Parse(graphJson);
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var aNodes = nodes.Where(n =>
                n.TryGetProperty("stateName", out var stateName)
                && string.Equals(stateName.GetString(), "A", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, aNodes.Count);
        var attempts = aNodes.Select(n => n.GetProperty("attempt").GetInt32()).OrderBy(x => x).ToArray();
        Assert.Equal([1, 2], attempts);
    }

    /// <summary>WaitTable に定義されたイベントキーが実行グラフの waitKey に記録されることを検証する。</summary>
    [Fact]
    public async Task Start_WhenStateHasWaitTableEntry_ExecutionGraphIncludesWaitKey()
    {
        // Arrange
        var def = CreateDefinitionWithWaitKey();
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);

        // Act
        var id = engine.Start(def);
        await Task.Delay(300);
        var graphJson = engine.ExportExecutionGraph(id);

        // Assert
        using var doc = JsonDocument.Parse(graphJson);
        var node = doc.RootElement.GetProperty("nodes").EnumerateArray()
            .First(n => string.Equals(n.GetProperty("stateName").GetString(), "WaitState", StringComparison.Ordinal));
        Assert.Equal("resume", node.GetProperty("waitKey").GetString());
    }

    /// <summary>Store.TryGetOutput が状態実行中に利用される経路を検証する。</summary>
    [Fact]
    public async Task Start_StateCanReadOtherStateOutputViaStore()
    {
        // Arrange
        var def = CreateDefinitionWithStoreReader();
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var id = engine.Start(def);

        // Act
        await Task.Delay(200);
        var snapshot = engine.GetSnapshot(id);

        // Assert: 完了にも失敗にもせず、遷移がないのでそのまま止まる
        Assert.NotNull(snapshot);
        Assert.False(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
    }

    /// <summary>状態実行中に StateContext の ExecutionId と StateName が参照される経路を検証する。</summary>
    [Fact]
    public async Task Start_StateCanReadExecutionIdAndStateName()
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
        var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
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

    /// <summary>
    /// <see cref="CreateDefinitionWithConditionalRoute"/> と同様だが、Route の出力を固定値ではなく
    /// <paramref name="routeOutputFromInput"/> で Start <c>input</c>（初期状態への入力）から生成する。
    /// </summary>
    private static CompiledWorkflowDefinition CreateDefinitionWithConditionalRouteFromStartInput(
        Func<object?, object?> routeOutputFromInput,
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
            Name = "ConditionalRouteFromStartInput",
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
                ["Route"] = new DefaultStateExecutor((_, input, _) => Task.FromResult(routeOutputFromInput(input))),
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

    /// <summary>テスト用: Start 時の <c>input</c> から score を読み取る（JSON 要素・整数の差異を吸収）。</summary>
    private static int ReadScoreFromStartInput(object? input)
    {
        if (input is not IReadOnlyDictionary<string, object?> dictionary)
            return 0;
        if (!dictionary.TryGetValue("score", out var raw) || raw is null)
            return 0;
        return raw switch
        {
            int i => i,
            long l => (int)l,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetInt32(),
            _ => 0
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
            var _ = ctx.ExecutionId;
            var _ = ctx.StateName;
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

    private static CompiledWorkflowDefinition CreateDefinitionWithStateRevisit()
    {
        var revisitAttemptForA = 0;
        return new CompiledWorkflowDefinition
        {
            Name = "Revisit",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["A"] = new Dictionary<string, TransitionTarget>(),
                ["B"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "A" } },
                ["End"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ConditionalTransitions = new Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>
            {
                ["A"] = new Dictionary<string, CompiledFactTransition>
                {
                    ["Completed"] = new CompiledFactTransition
                    {
                        Cases =
                        [
                            new CompiledTransitionCase
                            {
                                Order = 1,
                                DeclarationIndex = 0,
                                When = new ConditionExpressionDefinition { Path = "$.round", Op = "eq", Value = 1 },
                                Target = new TransitionTarget { Next = "B" }
                            }
                        ],
                        DefaultTarget = new TransitionTarget { Next = "End" }
                    }
                }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "A",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
            {
                ["A"] = new DefaultStateExecutor((_, _, _) =>
                {
                    revisitAttemptForA++;
                    return Task.FromResult<object?>(new Dictionary<string, object?> { ["round"] = revisitAttemptForA });
                }),
                ["B"] = DefaultStateExecutor.Create(new ImmediateState()),
                ["End"] = DefaultStateExecutor.Create(new ImmediateState())
            })
        };
    }

    private static CompiledWorkflowDefinition CreateDefinitionWithWaitKey() => new()
    {
        Name = "WithWaitKey",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["WaitState"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
        WaitTable = new Dictionary<string, string> { ["WaitState"] = "resume" },
        InitialState = "WaitState",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>
        {
            ["WaitState"] = DefaultStateExecutor.Create(new ImmediateState())
        })
    };

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }
}
