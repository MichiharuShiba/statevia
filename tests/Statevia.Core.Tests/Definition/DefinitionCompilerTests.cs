using Statevia.Core.Abstractions;
using Statevia.Core.Definition;
using Statevia.Core.Execution;
using Xunit;

namespace Statevia.Core.Tests.Definition;

public class DefinitionCompilerTests
{
    /// <summary>DefinitionCompiler が正しい遷移テーブル（on/Completed の next, end）を生成することを検証する。</summary>
    [Fact]
    public void Compile_ProducesCorrectTransitionTable()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "B" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert
        Assert.Equal("Test", compiled.Name);
        Assert.Equal(2, compiled.Transitions.Count);
        Assert.True(compiled.Transitions["A"]["Completed"].Next == "B");
        Assert.True(compiled.Transitions["B"]["Completed"].End);
    }

    /// <summary>DefinitionCompiler が正しい Fork テーブル（fork で並列開始する状態一覧）を生成することを検証する。</summary>
    [Fact]
    public void Compile_ProducesCorrectForkTable()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Fork = new[] { "A", "B" } } } },
                ["A"] = new StateDefinition(),
                ["B"] = new StateDefinition()
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert
        Assert.Single(compiled.ForkTable);
        Assert.Equal(new[] { "A", "B" }, compiled.ForkTable["Start"]);
    }

    /// <summary>DefinitionCompiler が参照されていない状態を初期状態として判定することを検証する。</summary>
    [Fact]
    public void Compile_DeterminesInitialState()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "End" } } },
                ["End"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert
        Assert.Equal("Start", compiled.InitialState);
    }
}
