using System.Collections.Generic;
using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
using Statevia.Core.Engine;
using Statevia.Core.Execution;
using Xunit;

namespace Statevia.Core.Tests.Definition;

public class DictionaryStateExecutorFactoryTests
{
    /// <summary>登録済み状態名で GetExecutor を呼ぶと、対応するエグゼキューターが返ることを検証する。</summary>
    [Fact]
    public void GetExecutor_ReturnsExecutor_WhenStateExists()
    {
        // Arrange
        var executor = DefaultStateExecutor.Create(new DummyState());
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["A"] = executor });

        // Act
        var result = factory.GetExecutor("A");

        // Assert
        Assert.Same(executor, result);
    }

    /// <summary>未登録の状態名で GetExecutor を呼ぶと null が返ることを検証する。</summary>
    [Fact]
    public void GetExecutor_ReturnsNull_WhenStateMissing()
    {
        // Arrange
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());

        // Act
        var result = factory.GetExecutor("NonExistent");

        // Assert
        Assert.Null(result);
    }

    /// <summary>GetExecutor が大文字小文字を区別しない（OrdinalIgnoreCase）ことを検証する。</summary>
    [Fact]
    public void GetExecutor_IsCaseInsensitive()
    {
        // Arrange
        var executor = DefaultStateExecutor.Create(new DummyState());
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>(StringComparer.OrdinalIgnoreCase) { ["Start"] = executor });

        // Act: 小文字で取得
        var result = factory.GetExecutor("start");

        // Assert: 同一インスタンスが返ること
        Assert.Same(executor, result);
    }

    /// <summary>取得したエグゼキューターで DummyState.ExecuteAsync が実行されることを検証する。</summary>
    [Fact]
    public async Task GetExecutor_ReturnedExecutorRunsDummyState()
    {
        // Arrange
        var state = new DummyState();
        var def = new CompiledWorkflowDefinition
        {
            Name = "Test",
            Transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
            {
                ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
            },
            ForkTable = new Dictionary<string, IReadOnlyList<string>>(),
            JoinTable = new Dictionary<string, IReadOnlyList<string>>(),
            WaitTable = new Dictionary<string, string>(),
            InitialState = "Start",
            StateExecutorFactory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor> { ["Start"] = DefaultStateExecutor.Create(state) })
        };
        var engine = new WorkflowEngine(new WorkflowEngineOptions { MaxParallelism = 1 });
        var id = engine.Start(def);

        // Act
        await Task.Delay(150);
        var snapshot = engine.GetSnapshot(id);

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsCompleted);
    }

    private sealed class DummyState : IState<Unit, Unit>
    {
        public Task<Unit> ExecuteAsync(StateContext ctx, Unit _, CancellationToken ct) => Task.FromResult(Unit.Value);
    }
}
