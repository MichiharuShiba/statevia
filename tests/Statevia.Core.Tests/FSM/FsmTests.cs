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
}
