using Statevia.Core.Definition;
using Statevia.Core.Definition.Validation;
using Xunit;

namespace Statevia.Core.Tests.Definition;

public class Level1ValidationTests
{
    /// <summary>状態が 0 件の定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_EmptyStates_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>()
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("at least one state", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>自己遷移（next が自分自身）は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_SelfTransition_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("self-transition", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>存在しない状態への参照（next）は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_UnknownStateReference_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "NonExistent" } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("unknown", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>整合性の取れた定義は Level1 検証を通過することを検証する。</summary>
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
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>空または空白の状態名は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_EmptyStateName_Fails()
    {
        // Arrange
        var states = new Dictionary<string, StateDefinition> { ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } } };
        states[""] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } };
        var def = new WorkflowDefinition { Workflow = new WorkflowMetadata { Name = "Test" }, States = states };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("State name cannot be empty", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Fork が存在しない状態を参照している定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_ForkReferencesUnknownState_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Fork = new[] { "A", "MissingState" } } } },
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Fork references unknown", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Join の allOf が存在しない状態を参照している定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_JoinReferencesUnknownState_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join1" } } },
                ["Join1"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A", "NotExist" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Join references unknown", StringComparison.OrdinalIgnoreCase));
    }
}
