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

    /// <summary>Join の allOf に存在しない状態名が含まれる場合でも HasCircularJoin は continue し、他に循環がなければ検証は通過することを検証する。</summary>
    [Fact]
    public void Validate_JoinAllOfContainsNonExistentState_DoesNotReportCircularJoin()
    {
        // Arrange: A join allOf [Missing]（Missing は States に存在しない）
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } },
                ["A"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "Missing" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level2Validator.Validate(def);

        // Assert: 循環は検出されない（Missing は TryGetValue でスキップ）。到達不能等のエラーはあり得る
        Assert.DoesNotContain(result.Errors, e => e.Contains("Circular join", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Fork 遷移を含むワークフローで到達可能性が正しく計算されることを検証する。</summary>
    [Fact]
    public void Validate_ForkTransition_ComputesReachabilityCorrectly()
    {
        // Arrange: Start → fork [A, B] → Join → End
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Fork = new[] { "A", "B" } } } },
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join" } } },
                ["B"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join" } } },
                ["Join"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A", "B" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { Next = "End" } } },
                ["End"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level2Validator.Validate(def);

        // Assert: Fork 遷移により A, B, Join, End が到達可能になる
        Assert.True(result.IsValid);
    }

    /// <summary>Join 状態が到達可能性計算に含まれることを検証する。</summary>
    [Fact]
    public void Validate_JoinState_IsIncludedInReachability()
    {
        // Arrange: Start → A → Join (allOf [A]) → End
        var def = new WorkflowDefinition
        {
            Workflow = new WorkflowMetadata { Name = "Test" },
            States = new Dictionary<string, StateDefinition>
            {
                ["Start"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "A" } } },
                ["A"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { Next = "Join" } } },
                ["Join"] = new StateDefinition { Join = new JoinDefinition { AllOf = new[] { "A" } }, On = new Dictionary<string, TransitionDefinition> { ["Joined"] = new TransitionDefinition { Next = "End" } } },
                ["End"] = new StateDefinition { On = new Dictionary<string, TransitionDefinition> { ["Completed"] = new TransitionDefinition { End = true } } }
            }
        };

        // Act
        var result = Level2Validator.Validate(def);

        // Assert: Join 状態が到達可能性に含まれる
        Assert.True(result.IsValid);
    }
}
