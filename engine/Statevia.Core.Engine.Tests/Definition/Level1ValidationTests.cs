using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

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

    /// <summary>input.path の形式が不正な定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_InvalidStateInputPath_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Input = new StateInputDefinition { Path = "payload.value" },
                    On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } }
                }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("input.path is invalid", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>input.path が "$.foo.bar" 形式なら Level1 検証を通過することを検証する。</summary>
    [Fact]
    public void Validate_ValidStateInputPath_Passes()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Input = new StateInputDefinition { Path = "$.foo.bar" },
                    On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } }
                }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>input マップ内の path が不正な場合は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_InvalidStateInputMapPath_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    Input = new StateInputDefinition
                    {
                        Values = new Dictionary<string, StateInputValueDefinition>
                        {
                            ["foo"] = new() { Path = "a.b" },
                            ["bar"] = new() { Literal = 1L }
                        }
                    },
                    On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } }
                }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("input.path is invalid", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>end: true を持つ遷移が一つもない定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_NoTerminalTransition_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new() { Next = "B" }
                    }
                },
                ["B"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new() { Next = "A" }
                    }
                }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("terminal transition", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>next と cases/default を同一遷移で混在させた定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_TransitionMixesLinearAndConditional_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Next = "B",
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "eq", Value = 1 },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "B" }
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cannot mix", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>cases を使う遷移で default が無い定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_CasesWithoutDefault_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "eq", Value = 1 },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ]
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("requires default", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>default が複数遷移（next と end）を併記する定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_DefaultTransitionWithMultipleTargets_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "eq", Value = 1 },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "B", End = true }
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("exactly one of next/fork/end", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>op: in の value が配列でない定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_ConditionInWithNonArrayValue_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "in", Value = 123 },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "B" }
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("op 'in' requires array", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>op: between の value が2要素配列でない定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_ConditionBetweenWithInvalidRange_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "between", Value = new[] { 1, 2, 3 } },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "B" }
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("op 'between' requires two-element", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>未サポートの when.op を含む定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_ConditionWithUnsupportedOperator_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "regex", Value = ".*" },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "B" }
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unsupported when.op", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>op: exists で value を指定した定義は Level1 検証で失敗することを検証する。</summary>
    [Fact]
    public void Validate_ConditionExistsWithValue_Fails()
    {
        // Arrange
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["A"] = new StateDefinition
                {
                    On = new Dictionary<string, TransitionDefinition>
                    {
                        ["Completed"] = new()
                        {
                            Cases =
                            [
                                new TransitionCaseDefinition
                                {
                                    When = new ConditionExpressionDefinition { Path = "$.x", Op = "exists", Value = true },
                                    Transition = new TransitionDefinition { Next = "B" }
                                }
                            ],
                            Default = new TransitionDefinition { Next = "B" }
                        }
                    }
                },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new() { End = true } } }
            }
        };

        // Act
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("op 'exists' must not define value", StringComparison.OrdinalIgnoreCase));
    }
}
