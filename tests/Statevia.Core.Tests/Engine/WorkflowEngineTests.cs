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
        Assert.NotNull(snapshot.ActiveStates);
    }

    /// <summary>存在するワークフロー ID で ExportExecutionGraph を呼ぶと JSON が返ることを検証する。</summary>
    [Fact]
    public void ExportExecutionGraph_ReturnsJson_WhenWorkflowExists()
    {
        var def = CreateMinimalDefinition();
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        var json = engine.ExportExecutionGraph(id);

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
