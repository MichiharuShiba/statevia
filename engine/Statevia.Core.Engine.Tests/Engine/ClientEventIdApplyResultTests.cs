using System;
using System.Collections.Generic;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary><see cref="ApplyResult"/> と clientEventId 冪等の Engine 挙動。</summary>
public class ClientEventIdApplyResultTests
{
    /// <summary>同一ワークフロー・同一 clientEventId の2回目の Publish は AlreadyApplied になる。</summary>
    [Fact]
    public async Task PublishEvent_SecondCallWithSameClientEventId_ReturnsAlreadyApplied()
    {
        // Arrange
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var workflowId = engine.Start(CreateMinimalDefinition());
        await Task.Delay(200).ConfigureAwait(true);
        var clientEventId = Guid.Parse("c3d4e5f6-a7b8-4901-c234-567890abcdef");

        // Act
        var first = engine.PublishEvent(workflowId, "AnyEvent", clientEventId);
        var second = engine.PublishEvent(workflowId, "AnyEvent", clientEventId);
        var otherId = engine.PublishEvent(workflowId, "AnyEvent", Guid.NewGuid());

        // Assert
        Assert.True(first.IsApplied);
        Assert.True(second.IsAlreadyApplied);
        Assert.True(otherId.IsApplied);
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
