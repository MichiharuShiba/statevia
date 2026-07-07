using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="StateDefinition.IsActionResolvable"/> の判定を検証する。</summary>
public sealed class StateDefinitionTests
{
    /// <summary>wait のみの状態は action 解決対象外。</summary>
    [Fact]
    public void IsActionResolvable_WaitOnly_ReturnsFalse()
    {
        // Arrange
        var state = new StateDefinition
        {
            Wait = new WaitDefinition { Event = "resume" },
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Resumed"] = new TransitionDefinition { End = true },
            },
        };

        // Act & Assert
        Assert.False(state.IsActionResolvable);
    }

    /// <summary>join のみの状態は action 解決対象外。</summary>
    [Fact]
    public void IsActionResolvable_JoinOnly_ReturnsFalse()
    {
        // Arrange
        var state = new StateDefinition
        {
            Join = new JoinDefinition { All = ["A", "B"] },
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Joined"] = new TransitionDefinition { End = true },
            },
        };

        // Act & Assert
        Assert.False(state.IsActionResolvable);
    }

    /// <summary>action 省略の通常状態は implicit noop 解決の対象。</summary>
    [Fact]
    public void IsActionResolvable_OmittedAction_ReturnsTrue()
    {
        // Arrange
        var state = new StateDefinition
        {
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionDefinition { End = true },
            },
        };

        // Act & Assert
        Assert.True(state.IsActionResolvable);
    }

    /// <summary>action 指定の通常状態は解決対象。</summary>
    [Fact]
    public void IsActionResolvable_ActionState_ReturnsTrue()
    {
        // Arrange
        var state = new StateDefinition
        {
            Action = "noop",
            On = new Dictionary<string, TransitionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Completed"] = new TransitionDefinition { End = true },
            },
        };

        // Act & Assert
        Assert.True(state.IsActionResolvable);
    }
}
