using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Tests.TestSupport;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary><see cref="ExecutionEngine.ResumeWaitNode"/> と WaitEventRoute 解決の検証。</summary>
public sealed class ResumeWaitNodeTests
{
    /// <summary>単一 Wait を ResumeWaitNode で再開し、route table の次状態へ進む。</summary>
    [Fact]
    public async Task ResumeWaitNode_CompletesWait_AndFollowsEventRoute()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);

        var waitNodeId = GetActiveWaitNodeId(engine, executionId);

        // Act
        engine.ResumeWaitNode(executionId, waitNodeId, "approve");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        var snapshot = engine.GetSnapshot(executionId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsCompleted);
        Assert.Contains(
            GetGraphNodes(engine, executionId),
            n => string.Equals(n.NodeName, "Approved", StringComparison.OrdinalIgnoreCase)
                && n.IsCompleted);
    }

    /// <summary>Fork 後の並列 Wait を nodeId ごとに Resume し、Join で完了する。</summary>
    [Fact]
    public async Task ResumeWaitNode_ForkParallelWaits_JoinCompletes()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        var executionId = engine.Start(CreateForkParallelWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 2);

        var waitNodes = GetGraphNodes(engine, executionId)
            .Where(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && !n.IsCompleted)
            .ToDictionary(n => n.NodeName, n => n.NodeId, StringComparer.OrdinalIgnoreCase);

        // Act
        engine.ResumeWaitNode(executionId, waitNodes["ManagerApproval"], "approve");
        engine.ResumeWaitNode(executionId, waitNodes["SecurityApproval"], "approve");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        var snapshot = engine.GetSnapshot(executionId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsCompleted);
        Assert.Contains(
            GetGraphNodes(engine, executionId),
            n => string.Equals(n.NodeName, "Join1", StringComparison.OrdinalIgnoreCase)
                && n.IsCompleted);
    }

    /// <summary>複数イベント Wait は Resume した event の route へ進む。</summary>
    [Fact]
    public async Task ResumeWaitNode_MultiEvent_FollowsSelectedEventRoute()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateMultiEventWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);
        var waitNodeId = GetActiveWaitNodeId(engine, executionId);

        // Act
        engine.ResumeWaitNode(executionId, waitNodeId, "reject");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        Assert.True(engine.GetSnapshot(executionId)!.IsCompleted);
        Assert.Contains(
            GetGraphNodes(engine, executionId),
            n => string.Equals(n.NodeName, "Rejected", StringComparison.OrdinalIgnoreCase)
                && n.IsCompleted);
        Assert.DoesNotContain(
            GetGraphNodes(engine, executionId),
            n => string.Equals(n.NodeName, "Approved", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>単一 Wait・単一イベントなら PublishEvent シムが ResumeWaitNode 相当に動く。</summary>
    [Fact]
    public async Task PublishEvent_Shim_ResumesSingleWaitWithSingleEvent()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);

        // Act
        engine.PublishEvent(executionId, "approve");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        Assert.True(engine.GetSnapshot(executionId)!.IsCompleted);
    }

    /// <summary>単一イベント Wait でも呼び出し eventName が許可イベントと一致しないとき PublishEvent は拒否する。</summary>
    [Fact]
    public async Task PublishEvent_Throws_WhenEventNameDoesNotMatchSoleAllowedEvent()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            engine.PublishEvent(executionId, "not-the-allowed-event"));
        Assert.Contains("does not match the sole allowed event", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountActiveWaits(engine, executionId));
    }

    /// <summary>複数イベント Wait では PublishEvent シムを拒否する。</summary>
    [Fact]
    public async Task PublishEvent_Throws_WhenWaitHasMultipleEvents()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateMultiEventWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => engine.PublishEvent(executionId, "approve"));
        Assert.Contains("exactly one event", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>アクティブ Wait が複数あるとき PublishEvent は拒否する。</summary>
    [Fact]
    public async Task PublishEvent_Throws_WhenMultipleActiveWaits()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 4);
        var executionId = engine.Start(CreateForkParallelWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 2);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => engine.PublishEvent(executionId, "approve"));
        Assert.Contains("exactly one active Wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>許可外イベントの ResumeWaitNode は拒否する。</summary>
    [Fact]
    public async Task ResumeWaitNode_Throws_WhenEventNotAllowed()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);
        var waitNodeId = GetActiveWaitNodeId(engine, executionId);

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            engine.ResumeWaitNode(executionId, waitNodeId, "reject"));
        Assert.Contains("not allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>前後空白付き eventName でも ResumeWaitNode は許可イベントとして受理する。</summary>
    [Fact]
    public async Task ResumeWaitNode_AcceptsEventNameWithSurroundingWhitespace()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);
        var waitNodeId = GetActiveWaitNodeId(engine, executionId);

        // Act
        engine.ResumeWaitNode(executionId, waitNodeId, "  approve  ");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        Assert.True(engine.GetSnapshot(executionId)!.IsCompleted);
    }

    /// <summary>Wait output の event 名に空白があっても route table で遷移できる。</summary>
    [Fact]
    public async Task ResumeWaitNode_FollowsRoute_WhenOutputEventHasSurroundingWhitespace()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinitionWithPaddedOutputEvent());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);
        var waitNodeId = GetActiveWaitNodeId(engine, executionId);

        // Act
        engine.ResumeWaitNode(executionId, waitNodeId, "approve");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        Assert.True(engine.GetSnapshot(executionId)!.IsCompleted);
        Assert.Contains(
            GetGraphNodes(engine, executionId),
            n => string.Equals(n.NodeName, "Approved", StringComparison.OrdinalIgnoreCase)
                && n.IsCompleted);
    }

    /// <summary>
    /// Wait ルートの Next が空のとき FSM（on.Completed）へフォールバックして遷移することを検証する。
    /// </summary>
    [Fact]
    public async Task ResumeWaitNode_FallsBackToFsm_WhenRouteNextIsEmpty()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinitionWithEmptyRouteNext());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);
        var waitNodeId = GetActiveWaitNodeId(engine, executionId);

        // Act
        engine.ResumeWaitNode(executionId, waitNodeId, "approve");
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        Assert.True(engine.GetSnapshot(executionId)!.IsCompleted);
        Assert.Contains(
            GetGraphNodes(engine, executionId),
            n => string.Equals(n.NodeName, "Approved", StringComparison.OrdinalIgnoreCase)
                && n.IsCompleted);
    }

    private static CompiledWorkflowDefinition CreateMultiEventWaitDefinition() => new()
    {
        Name = "MultiEventWait",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase),
            ["Approved"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { End = true },
            },
            ["Rejected"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { End = true },
            },
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["approve"] = new WaitEventRouteDefinition { Next = "Approved" },
                ["reject"] = new WaitEventRouteDefinition { Next = "Rejected" },
            },
        },
        InitialState = "ApproveTask",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = DefaultStateExecutor.Create(new NodeScopedWaitState("approve", "reject")),
            ["Approved"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["Rejected"] = DefaultStateExecutor.Create(new ImmediateState()),
        }),
    };

    private static CompiledWorkflowDefinition CreateSingleWaitDefinition() => new()
    {
        Name = "SingleWait",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase),
            ["Approved"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { End = true },
            },
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["approve"] = new WaitEventRouteDefinition { Next = "Approved" },
            },
        },
        InitialState = "ApproveTask",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = DefaultStateExecutor.Create(new NodeScopedWaitState("approve")),
            ["Approved"] = DefaultStateExecutor.Create(new ImmediateState()),
        }),
    };

    /// <summary>Wait output の event に意図的に空白を付けて route 解決の Trim を検証する定義。</summary>
    private static CompiledWorkflowDefinition CreateSingleWaitDefinitionWithPaddedOutputEvent() => new()
    {
        Name = "SingleWaitPaddedOutput",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase),
            ["Approved"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { End = true },
            },
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["approve"] = new WaitEventRouteDefinition { Next = "Approved" },
            },
        },
        InitialState = "ApproveTask",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = DefaultStateExecutor.Create(new PaddedOutputWaitState("approve")),
            ["Approved"] = DefaultStateExecutor.Create(new ImmediateState()),
        }),
    };

    /// <summary>
    /// ルート Next が空で、遷移先は Transitions の Completed にのみある定義（旧 wait.event + on.Completed 相当）。
    /// </summary>
    private static CompiledWorkflowDefinition CreateSingleWaitDefinitionWithEmptyRouteNext() => new()
    {
        Name = "SingleWaitEmptyRouteNext",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { Next = "Approved" },
            },
            ["Approved"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { End = true },
            },
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["approve"] = new WaitEventRouteDefinition { Next = "   " },
            },
        },
        InitialState = "ApproveTask",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApproveTask"] = DefaultStateExecutor.Create(new NodeScopedWaitState("approve")),
            ["Approved"] = DefaultStateExecutor.Create(new ImmediateState()),
        }),
    };

    private static CompiledWorkflowDefinition CreateForkParallelWaitDefinition() => new()
    {
        Name = "ForkParallelWait",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Start"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { Fork = ["ManagerApproval", "SecurityApproval"] },
            },
            ["ManagerApproval"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { Next = "Join1" },
            },
            ["SecurityApproval"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { Next = "Join1" },
            },
            ["Join1"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Joined"] = new TransitionTarget { End = true },
            },
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Start"] = ["ManagerApproval", "SecurityApproval"],
        },
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Join1"] = ["ManagerApproval", "SecurityApproval"],
        },
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ManagerApproval"] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["approve"] = new WaitEventRouteDefinition { Next = "Join1" },
            },
            ["SecurityApproval"] = new Dictionary<string, WaitEventRouteDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["approve"] = new WaitEventRouteDefinition { Next = "Join1" },
            },
        },
        InitialState = "Start",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["Start"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["ManagerApproval"] = DefaultStateExecutor.Create(new NodeScopedWaitState("approve")),
            ["SecurityApproval"] = DefaultStateExecutor.Create(new NodeScopedWaitState("approve")),
        }),
    };

    private static IReadOnlyList<(string NodeId, string NodeName, string? NodeType, bool IsCompleted)> GetGraphNodes(
        ExecutionEngine engine,
        string executionId)
    {
        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(executionId));
        if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
        {
            return [];
        }

        return nodes.EnumerateArray()
            .Select(n =>
            {
                var nodeId = n.GetProperty("nodeId").GetString() ?? string.Empty;
                var nodeName = n.GetProperty("nodeName").GetString() ?? string.Empty;
                var nodeType = n.TryGetProperty("nodeType", out var nt) ? nt.GetString() : null;
                var isCompleted = n.TryGetProperty("completedAt", out var completedAt)
                    && completedAt.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
                return (nodeId, nodeName, nodeType, isCompleted);
            })
            .ToList();
    }

    private static int CountActiveWaits(ExecutionEngine engine, string executionId) =>
        GetGraphNodes(engine, executionId)
            .Count(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && !n.IsCompleted);

    private static string GetActiveWaitNodeId(ExecutionEngine engine, string executionId) =>
        GetGraphNodes(engine, executionId)
            .Single(n => string.Equals(n.NodeType, "Wait", StringComparison.OrdinalIgnoreCase)
                && !n.IsCompleted)
            .NodeId;

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20).ConfigureAwait(true);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    private sealed class ImmediateState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) =>
            Task.FromResult(Unit.Value);
    }

    /// <summary>ノードスコープでイベントを待機し、監査用 output.event を返すテスト用 State。</summary>
    private sealed class NodeScopedWaitState(params string[] eventNames) : IState<Unit, IReadOnlyDictionary<string, string>>
    {
        public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
            StateContext ctx,
            Unit _,
            CancellationToken ct)
        {
            var nodeId = string.IsNullOrWhiteSpace(ctx.NodeId) ? ctx.StateName : ctx.NodeId;
            var eventName = await ctx.Events.WaitForEventAsync(nodeId, eventNames, ct);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["event"] = eventName,
            };
        }
    }

    /// <summary>受信イベント名を空白付きで output に載せ、route ルックアップの Trim を検証する。</summary>
    private sealed class PaddedOutputWaitState(params string[] eventNames) : IState<Unit, IReadOnlyDictionary<string, string>>
    {
        public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
            StateContext ctx,
            Unit _,
            CancellationToken ct)
        {
            var nodeId = string.IsNullOrWhiteSpace(ctx.NodeId) ? ctx.StateName : ctx.NodeId;
            var eventName = await ctx.Events.WaitForEventAsync(nodeId, eventNames, ct);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["event"] = $"  {eventName}  ",
            };
        }
    }
}
