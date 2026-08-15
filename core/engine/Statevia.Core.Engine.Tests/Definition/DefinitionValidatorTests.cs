using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

public class DefinitionValidatorTests
{
    /// <summary>Level1 で失敗する定義では Level1 の検証結果が返り、Level2 は実行されないことを検証する。</summary>
    [Fact]
    public void Validate_WhenLevel1Fails_ReturnsLevel1Result()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "Test",
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
            Name = "Test",
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
            Name = "Test",
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

    /// <summary>
    /// Structural 失敗時は Reachability を実行せず、指摘に RuleId が付くことを検証する。
    /// </summary>
    [Fact]
    public void Validate_WhenStructuralFails_SkipsReachabilityAndSetsRuleId()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "Test",
            States = new Dictionary<string, StateDefinition>()
        };

        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Equal("Structural.AtLeastOneState", result.Issues[0].RuleId);
        Assert.DoesNotContain(result.Issues, i => i.RuleId.StartsWith("Reachability.", StringComparison.Ordinal));
    }

    /// <summary>パイプラインに Structural と Reachability の細ルールが登録されていることを検証する。</summary>
    [Fact]
    public void Pipeline_RegistersFineGrainedRulesByPhase()
    {
        // Arrange
        var rules = ValidationPipeline.Rules;

        // Act
        var structural = rules.Where(r => r.Phase == ValidationPhase.Structural).Select(r => r.RuleId).ToArray();
        var reachability = rules.Where(r => r.Phase == ValidationPhase.Reachability).Select(r => r.RuleId).ToArray();

        // Assert
        Assert.Contains("Structural.AtLeastOneState", structural);
        Assert.Contains("Structural.StateGraph", structural);
        Assert.Contains("Reachability.UnreachableStates", reachability);
        Assert.Contains("Reachability.CircularJoin", reachability);
        var forkRegion = rules.Where(r => r.Phase == ValidationPhase.ForkRegion).Select(r => r.RuleId).ToArray();
        Assert.Contains("ForkRegion.Snapshot", forkRegion);
        Assert.Contains("ForkRegion.NestedCrossBranch", forkRegion);
    }

    /// <summary>定義が null のとき ArgumentNullException が発生することを検証する。</summary>
    [Fact]
    public void Validate_WhenDefinitionIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => DefinitionValidator.Validate(null!));
    }

    /// <summary>
    /// 条件遷移（cases/default）でのみ参照される状態も到達可能として扱われ、検証が通過することを検証する。
    /// </summary>
    [Fact]
    public void Validate_WhenConditionalReferencesExist_ReturnsValidResult()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Name = "ConditionalReachable",
            States = new Dictionary<string, StateDefinition>
            {
                ["Route"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "gt", Value = 0 },
                                    Transition = new TransitionDefinition { Next = "High" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "Low" }
                        }
                    }
                },
                ["High"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { End = true }
                    }
                },
                ["Low"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new TransitionDefinition { End = true }
                    }
                }
            }
        };
        // Act
        var result = DefinitionValidator.Validate(def);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
