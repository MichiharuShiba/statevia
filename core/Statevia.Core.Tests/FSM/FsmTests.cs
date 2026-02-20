using Statevia.Core.Abstractions;
using Statevia.Core.FSM;
using Xunit;

namespace Statevia.Core.Tests.FSM;

public class FsmTests
{
    /// <summary>遷移が存在しない (状態, 事実) の組み合わせでは HasTransition が false になることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsNone_WhenNoTransition()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } }
        };
        var fsm = new TransitionTable(transitions);

        // Act: A で Failed は定義されていない
        var result = fsm.Evaluate("A", "Failed");

        // Assert
        Assert.False(result.HasTransition);
    }

    /// <summary>next 遷移が存在する (状態, 事実) では Next に遷移先が返ることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsToNext_WhenTransitionExists()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } }
        };
        var fsm = new TransitionTable(transitions);

        // Act
        var result = fsm.Evaluate("A", "Completed");

        // Assert
        Assert.True(result.HasTransition);
        Assert.Equal("B", result.Next);
    }

    /// <summary>fork 遷移が存在する (状態, 事実) では Fork に並列開始する状態一覧が返ることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsToFork_WhenForkTransition()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["Start"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Fork = new[] { "A", "B" } } }
        };
        var fsm = new TransitionTable(transitions);

        // Act
        var result = fsm.Evaluate("Start", "Completed");

        // Assert
        Assert.True(result.HasTransition);
        Assert.Equal(new[] { "A", "B" }, result.Fork);
    }

    /// <summary>end 遷移が存在する (状態, 事実) では End が true になることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsToEnd_WhenEndTransition()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["End"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { End = true } }
        };
        var fsm = new TransitionTable(transitions);

        // Act
        var result = fsm.Evaluate("End", "Completed");

        // Assert
        Assert.True(result.HasTransition);
        Assert.True(result.End);
    }

    /// <summary>存在しない状態名で Evaluate を呼ぶと None が返ることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsNone_WhenStateNotInTable()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } }
        };
        var fsm = new TransitionTable(transitions);

        // Act
        var result = fsm.Evaluate("NonExistent", "Completed");

        // Assert
        Assert.False(result.HasTransition);
    }

    /// <summary>状態は存在するが fact が遷移に存在しない場合は None が返ることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsNone_WhenFactNotInStateTransitions()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["A"] = new Dictionary<string, TransitionTarget> { ["Completed"] = new TransitionTarget { Next = "B" } }
        };
        var fsm = new TransitionTable(transitions);

        // Act
        var result = fsm.Evaluate("A", "UnknownFact");

        // Assert
        Assert.False(result.HasTransition);
    }

    /// <summary>遷移先が Next/End/Fork のいずれでもない場合は None が返ることを検証する。</summary>
    [Fact]
    public void Evaluate_ReturnsNone_WhenTargetHasNoNextEndOrFork()
    {
        // Arrange
        var transitions = new Dictionary<string, IReadOnlyDictionary<string, TransitionTarget>>
        {
            ["A"] = new Dictionary<string, TransitionTarget> { ["Done"] = new TransitionTarget { Next = null, End = false, Fork = null } }
        };
        var fsm = new TransitionTable(transitions);

        // Act
        var result = fsm.Evaluate("A", "Done");

        // Assert
        Assert.False(result.HasTransition);
    }
}
