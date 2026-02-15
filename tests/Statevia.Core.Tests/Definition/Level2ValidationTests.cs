using Statevia.Core.Definition;
using Statevia.Core.Definition.Validation;
using Xunit;

namespace Statevia.Core.Tests.Definition;

public class Level2ValidationTests
{
    /// <summary>到達不能な状態が存在する定義は Level2 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_UnreachableState_Fails()
    {
        // Arrange: Orphan は A→B のグラフから到達不能
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "B" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } },
                ["Orphan"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level2Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("unreachable", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>到達可能性の取れた定義は Level2 検証を通過することを検証する。</summary>
    [Fact]
    public void Validate_ValidDefinition_Passes()
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

        // Act
        var result = Level2Validator.Validate(def);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>循環 Join（A が B を待ち、B が A を待つ等）がある定義は Level2 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_CircularJoin_Fails()
    {
        // Arrange: A join allOf [B], B join allOf [A]
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } },
                ["A"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "B" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } },
                ["B"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level2Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Circular join", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Join が自分自身を allOf に含む場合も循環として検出することを検証する。</summary>
    [Fact]
    public void Validate_SelfJoinCircular_Fails()
    {
        // Arrange: A join allOf [A]
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } },
                ["A"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level2Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Circular join", StringComparison.OrdinalIgnoreCase));
    }
}
