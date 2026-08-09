using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Tests.TestSupport;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary>Export / Unload / Import / Resume のチェックポイント往復を検証する。</summary>
public sealed class ExecutionRuntimeCheckpointTests
{
    /// <summary>
    /// ステップ完了時点（次遷移前）の checkpoint を Import すると、完了フロンティアから継続して終端に達する。
    /// </summary>
    [Fact]
    public async Task ImportCheckpoint_CompletedFrontierWithoutOutgoing_ContinuesToEnd()
    {
        // Arrange: A 完了通知中に Export+Unload し、ProcessFact 前の断面を固定する。
        var definition = CreateTwoStepDefinition();
        using var engine1 = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        ExecutionRuntimeCheckpoint? captured = null;
        engine1.SetNodeCompletedHandler(executionId =>
        {
            if (captured is not null)
                return Task.CompletedTask;

            captured = engine1.ExportCheckpoint(executionId);
            engine1.Unload(executionId);
            return Task.CompletedTask;
        });

        var executionId = engine1.Start(definition);
        await WaitUntilAsync(() => captured is not null);

        Assert.NotNull(captured);
        Assert.Empty(captured!.PendingWaits);
        Assert.Contains(captured.Graph.Nodes, n =>
            string.Equals(n.NodeName, "StepA", StringComparison.OrdinalIgnoreCase)
            && n.CompletedAt is not null);
        Assert.DoesNotContain(captured.Graph.Edges, e =>
            captured.Graph.Nodes.Any(n =>
                string.Equals(n.NodeName, "StepA", StringComparison.OrdinalIgnoreCase)
                && string.Equals(n.NodeId, e.From, StringComparison.OrdinalIgnoreCase)));

        // Act
        using var engine2 = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        engine2.ImportCheckpoint(definition, captured);
        await WaitUntilAsync(() => engine2.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        var snapshot = engine2.GetSnapshot(executionId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsCompleted);
    }

    /// <summary>Wait 到達後に Export→Unload→別 Engine で Import→Resume できる。</summary>
    [Fact]
    public async Task ExportUnloadImport_ResumeWait_CompletesExecution()
    {
        // Arrange
        var definition = CreateSingleWaitDefinition();
        using var engine1 = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        var executionId = engine1.Start(definition);
        await WaitUntilAsync(() => engine1.ExportCheckpoint(executionId)?.PendingWaits.Count == 1);

        var checkpoint = engine1.ExportCheckpoint(executionId);
        Assert.NotNull(checkpoint);
        var waitNodeId = checkpoint!.PendingWaits[0].NodeId;

        // Act
        Assert.True(engine1.Unload(executionId));
        Assert.Null(engine1.GetSnapshot(executionId));

        using var engine2 = ExecutionEngineTestHarness.Create(maxParallelism: 2);
        engine2.ImportCheckpoint(definition, checkpoint);
        // Wait 登録を待たず即 Resume（pending resume バッファ経路）。
        engine2.ResumeWaitNode(executionId, waitNodeId, "approve");
        await WaitUntilAsync(() => engine2.GetSnapshot(executionId)?.IsCompleted == true);

        // Assert
        var snapshot = engine2.GetSnapshot(executionId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsCompleted);
    }

    private static CompiledWorkflowDefinition CreateTwoStepDefinition() => new()
    {
        Name = "TwoStep",
        Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["StepA"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { Next = "StepB" },
            },
            ["StepB"] = new Dictionary<string, TransitionTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionTarget { End = true },
            },
        },
        ForkTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        JoinTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
        WaitEventRouteTable = new Dictionary<string, IReadOnlyDictionary<string, WaitEventRouteDefinition>>(StringComparer.OrdinalIgnoreCase),
        InitialState = "StepA",
        StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase)
        {
            ["StepA"] = DefaultStateExecutor.Create(new ImmediateState()),
            ["StepB"] = DefaultStateExecutor.Create(new ImmediateState()),
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

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"Condition not met within {timeoutMs}ms.");
    }

    private sealed class ImmediateState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult(input);
    }

    private sealed class NodeScopedWaitState : IState<object?, object?>
    {
        private readonly string[] _events;

        public NodeScopedWaitState(params string[] events) => _events = events;

        public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
        {
            var nodeId = string.IsNullOrWhiteSpace(ctx.NodeId) ? ctx.StateName : ctx.NodeId;
            var eventName = await ctx.Events.WaitForEventAsync(nodeId!, _events, ct).ConfigureAwait(false);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["event"] = eventName };
        }
    }
}
