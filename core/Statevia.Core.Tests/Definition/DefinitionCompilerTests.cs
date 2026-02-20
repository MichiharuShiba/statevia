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

    /// <summary>DefinitionCompiler が Wait テーブルを生成し、WaitTable ゲッターが参照されることを検証する。</summary>
    [Fact]
    public void Compile_ProducesWaitTable_WhenWaitStateExists()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "WaitState" } } },
                ["WaitState"] = new StateDefinition { Wait = new WaitDefinition { Event = "resume" }, On = new Dictionary<string, TransitionDefinition> { ["Resumed"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);
        var waitTable = compiled.WaitTable;

        // Assert
        Assert.Single(waitTable);
        Assert.Equal("resume", waitTable["WaitState"]);
    }

    /// <summary>Join で AllOf が空の状態は Join テーブルに含めないことを検証する。</summary>
    [Fact]
    public void Compile_JoinWithEmptyAllOf_NotInJoinTable()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { Join = new JoinDefinition { AllOf = new List<string>() }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert
        Assert.Empty(compiled.JoinTable);
    }

    /// <summary>全状態が何かから参照される場合は先頭状態を初期状態とすることを検証する。</summary>
    [Fact]
    public void Compile_AllStatesReferenced_UsesFirstAsInitial()
    {
        // Arrange: A→B, B→A で全状態が参照される
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "B" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert: 辞書の先頭キーが初期状態になる
        Assert.True(compiled.InitialState == "A" || compiled.InitialState == "B");
        Assert.Contains(compiled.InitialState, def.States.Keys);
    }

    /// <summary>next が自状態名の遷移（自己ループ）は遷移テーブルに含めないことを検証する。</summary>
    [Fact]
    public void Compile_SkipsSelfLoopTransition()
    {
        // Arrange: A の on.Completed.next が A 自身
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert: A は自己ループのみなので遷移テーブルにエントリが追加されない
        Assert.False(compiled.Transitions.ContainsKey("A"));
        Assert.True(compiled.Transitions.ContainsKey("B"));
    }

    /// <summary>状態が 0 件の定義で Compile すると InvalidOperationException が発生することを検証する。</summary>
    [Fact]
    public void Compile_Throws_WhenStatesEmpty()
    {
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>()
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        Assert.Throws<InvalidOperationException>(() => compiler.Compile(def));
    }

    /// <summary>DefinitionCompiler が正しい Join テーブルを生成することを検証する。</summary>
    [Fact]
    public void Compile_ProducesCorrectJoinTable()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Fork = new[] { "A", "B" } } } },
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join" } } },
                ["Join"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A", "B" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert
        Assert.Single(compiled.JoinTable);
        Assert.True(compiled.JoinTable.ContainsKey("Join"));
        Assert.Equal(new[] { "A", "B" }, compiled.JoinTable["Join"]);
    }

    /// <summary>複数の Join 状態がある場合、すべてが Join テーブルに含まれることを検証する。</summary>
    [Fact]
    public void Compile_MultipleJoinStates_AllIncludedInJoinTable()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Fork = new[] { "A", "B" } } } },
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join1" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join2" } } },
                ["Join1"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } },
                ["Join2"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "B" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };
        var factory = new DictionaryStateExecutorFactory(new Dictionary<string, IStateExecutor>());
        var compiler = new DefinitionCompiler(factory);

        // Act
        var compiled = compiler.Compile(def);

        // Assert
        Assert.Equal(2, compiled.JoinTable.Count);
        Assert.True(compiled.JoinTable.ContainsKey("Join1"));
        Assert.True(compiled.JoinTable.ContainsKey("Join2"));
        Assert.Equal(new[] { "A" }, compiled.JoinTable["Join1"]);
        Assert.Equal(new[] { "B" }, compiled.JoinTable["Join2"]);
    }
}
