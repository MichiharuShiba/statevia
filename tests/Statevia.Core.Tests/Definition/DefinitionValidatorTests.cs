using Statevia.Core.Definition;
using Statevia.Core.Definition.Validation;
using Xunit;

namespace Statevia.Core.Tests.Definition;

public class DefinitionValidatorTests
{
    /// <summary>Level1 で失敗する定義では Level1 の検証結果が返り、Level2 は実行されないことを検証する。</summary>
    [Fact]
    public void Validate_WhenLevel1Fails_ReturnsLevel1Result()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>()
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("at least one state", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Level1 は通過するが Level2 で失敗する定義では Level2 の検証結果が返ることを検証する。</summary>
    [Fact]
    public void Validate_WhenLevel1PassesLevel2Fails_ReturnsLevel2Result()
    {
        // Arrange: 参照整合性は取れているが、Orphan が到達不能
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
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("unreachable", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Level1・Level2 ともに通過する定義では有効な結果が返ることを検証する。</summary>
    [Fact]
    public void Validate_WhenBothPass_ReturnsValidResult()
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
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>定義が null のとき ArgumentNullException が発生することを検証する。</summary>
    [Fact]
    public void Validate_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DefinitionValidator.Validate(null!));
    }
}
