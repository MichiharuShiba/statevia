using System.Collections.Generic;
using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
using Statevia.Core.Engine;
using Statevia.Core.Execution;
using Xunit;

namespace Statevia.Core.Tests.Engine;

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
