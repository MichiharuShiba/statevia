using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Tests.TestSupport;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary><see cref="ApplyResult"/> と clientEventId 冪等の Engine 挙動。</summary>
public class ClientEventIdApplyResultTests
{
    /// <summary>同一 Wait・同一 clientEventId の 2 回目 Publish は AlreadyApplied になる。</summary>
    [Fact]
    public async Task PublishEvent_SecondCallWithSameClientEventId_ReturnsAlreadyApplied()
    {
        // Arrange
        using var engine = ExecutionEngineTestHarness.Create(maxParallelism: 1);
        var executionId = engine.Start(CreateSingleWaitDefinition());
        await WaitUntilAsync(() => CountActiveWaits(engine, executionId) == 1);
        var clientEventId = Guid.Parse("c3d4e5f6-a7b8-4901-c234-567890abcdef");

        // Act
        var first = engine.PublishEvent(executionId, "approve", clientEventId);
        await WaitUntilAsync(() => engine.GetSnapshot(executionId)?.IsCompleted == true);
        var second = engine.PublishEvent(executionId, "approve", clientEventId);

        // Assert
        Assert.True(first.IsApplied);
        Assert.True(second.IsAlreadyApplied);
    }

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

    private static int CountActiveWaits(ExecutionEngine engine, string executionId)
    {
        using var doc = JsonDocument.Parse(engine.ExportExecutionGraph(executionId));
        if (!doc.RootElement.TryGetProperty("nodes", out var nodes))
        {
            return 0;
        }

        return nodes.EnumerateArray()
            .Count(n =>
                string.Equals(n.GetProperty("nodeType").GetString(), "Wait", StringComparison.OrdinalIgnoreCase)
                && (!n.TryGetProperty("completedAt", out var completedAt)
                    || completedAt.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined));
    }

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

    private sealed class NodeScopedWaitState(params string[] eventNames) : IState<Unit, IReadOnlyDictionary<string, string>>
    {
        public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
            StateContext ctx,
            Unit _,
            CancellationToken ct)
        {
            var nodeId = string.IsNullOrWhiteSpace(ctx.NodeId) ? ctx.StateName : ctx.NodeId;
            var eventName = await ctx.Events.WaitForEventAsync(nodeId, eventNames, ct).ConfigureAwait(false);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["event"] = eventName,
            };
        }
    }
}
