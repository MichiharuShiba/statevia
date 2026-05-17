using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.FSM;
using Xunit;

namespace Statevia.Core.Engine.Tests.Engine;

/// <summary><see cref="OutputConditionEvaluator"/> の条件遷移評価。</summary>
public class OutputConditionEvaluatorTests
{
    /// <summary>線形ターゲットのみのとき Linear 解決になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_LinearTarget_ReturnsLinearResolution()
    {
        // Arrange
        var compiled = new CompiledFactTransition
        {
            LinearTarget = new TransitionTarget { Next = "Done" }
        };

        // Act
        var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output: null, onPathWarning: null);

        // Assert
        Assert.True(transition.HasTransition);
        Assert.Equal("Done", transition.Next);
        Assert.Equal(ConditionRoutingResolutions.Linear, diagnostics.Resolution);
    }

    /// <summary>eq が真の case に一致すると MatchedCase になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_EqCase_MatchesFirstCase()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["status"] = "ok" };
        var compiled = Conditional(
            [Case("$.status", "eq", "ok", "BranchA")],
            defaultTarget: new TransitionTarget { Next = "Fallback" });

        // Act
        var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("BranchA", transition.Next);
        Assert.Equal(ConditionRoutingResolutions.MatchedCase, diagnostics.Resolution);
        Assert.Equal(0, diagnostics.MatchedCaseIndex);
    }

    /// <summary>どの case も一致しないとき default へフォールバックすることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_NoCaseMatch_UsesDefaultFallback()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["n"] = 1 };
        var compiled = Conditional(
            [Case("$.n", "gt", 10, "High")],
            defaultTarget: new TransitionTarget { Next = "Low" });

        // Act
        var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("Low", transition.Next);
        Assert.Equal(ConditionRoutingResolutions.DefaultFallback, diagnostics.Resolution);
        Assert.Null(diagnostics.MatchedCaseIndex);
        Assert.Equal("condition_false", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    /// <summary>default も無いとき遷移なしになることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_NoMatchAndNoDefault_ReturnsNone()
    {
        // Arrange
        var compiled = Conditional([Case("$.missing", "exists", null, "X")]);

        // Act
        var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", new Dictionary<string, object?>(), onPathWarning: null);

        // Assert
        Assert.False(transition.HasTransition);
        Assert.Equal(ConditionRoutingResolutions.NoTransition, diagnostics.Resolution);
        Assert.NotEmpty(diagnostics.EvaluationErrors);
    }

    /// <summary>exists / in / between / ne など主要演算子を検証する。</summary>
    [Theory]
    [InlineData("exists", null, "Present", true)]
    [InlineData("ne", "old", "new", true)]
    [InlineData("gte", 10, 10, true)]
    [InlineData("lt", 5, 3, true)]
    [InlineData("in", new object[] { "a", "b" }, "a", true)]
    [InlineData("between", new object[] { 1, 10 }, 5, true)]
    public void EvaluateDetailed_Operators_MatchExpected(
        string op,
        object? expectedValue,
        object? actualLeaf,
        bool shouldMatch)
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["v"] = actualLeaf };
        var compiled = Conditional([Case("$.v", op, expectedValue, "Hit")]);

        // Act
        var (transition, _) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        if (shouldMatch)
        {
            Assert.Equal("Hit", transition.Next);
        }
        else
        {
            Assert.False(transition.HasTransition);
        }
    }

    /// <summary>数値 1 と真偽 true の eq が成立することを検証する（型ゆらぎ）。</summary>
    [Fact]
    public void EvaluateDetailed_Eq_CoercesIntegralOneToTrue()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["flag"] = 1L };
        var compiled = Conditional([Case("$.flag", "eq", true, "Yes")]);

        // Act
        var (transition, _) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("Yes", transition.Next);
    }

    /// <summary>未対応 op では unsupported_op 理由が記録されることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_UnsupportedOp_RecordsReason()
    {
        // Arrange
        var compiled = Conditional([Case("$.x", "unknown-op", 1, "X")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", new Dictionary<string, object?> { ["x"] = 1 }, onPathWarning: null);

        // Assert
        Assert.Equal("unsupported_op", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    /// <summary>in の右辺がコレクションでないとき in_operand_not_collection になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_InWithScalarOperand_RecordsInOperandNotCollection()
    {
        // Arrange
        var compiled = Conditional([Case("$.v", "in", "not-a-collection", "X")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", new Dictionary<string, object?> { ["v"] = 1 }, onPathWarning: null);

        // Assert
        Assert.Equal("in_operand_not_collection", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    /// <summary><see cref="OutputConditionEvaluator.Evaluate"/> が詳細評価と同じ遷移を返すことを検証する。</summary>
    [Fact]
    public void Evaluate_DelegatesToEvaluateDetailed()
    {
        // Arrange
        var compiled = new CompiledFactTransition
        {
            LinearTarget = new TransitionTarget { Next = "Done" }
        };

        // Act
        var transition = OutputConditionEvaluator.Evaluate(compiled, output: null, onPathWarning: null);

        // Assert
        Assert.Equal("Done", transition.Next);
    }

    /// <summary>線形ターゲットが End のとき終端遷移になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_LinearEnd_ReturnsEndTransition()
    {
        // Arrange
        var compiled = new CompiledFactTransition
        {
            LinearTarget = new TransitionTarget { End = true }
        };

        // Act
        var (transition, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output: null, onPathWarning: null);

        // Assert
        Assert.True(transition.End);
        Assert.Equal(ConditionRoutingResolutions.Linear, diagnostics.Resolution);
    }

    /// <summary>線形ターゲットが Fork のとき fork 遷移になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_LinearFork_ReturnsForkTransition()
    {
        // Arrange
        var compiled = new CompiledFactTransition
        {
            LinearTarget = new TransitionTarget { Fork = new[] { "A", "B" } }
        };

        // Act
        var (transition, _) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output: null, onPathWarning: null);

        // Assert
        Assert.Equal(new[] { "A", "B" }, transition.Fork);
    }

    /// <summary>lte が成立するとき case に一致することを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_Lte_MatchesWhenLessOrEqual()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["n"] = 5 };
        var compiled = Conditional([Case("$.n", "lte", 5, "Ok")]);

        // Act
        var (transition, _) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("Ok", transition.Next);
    }

    /// <summary>パスが存在しない eq は path_not_found になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_EqOnMissingPath_RecordsPathNotFound()
    {
        // Arrange
        var compiled = Conditional([Case("$.missing", "eq", 1, "X")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", new Dictionary<string, object?>(), onPathWarning: null);

        // Assert
        Assert.Equal("path_not_found", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    /// <summary>比較演算で null オペランドのとき compare_operand_null になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_CompareWithNullOperand_RecordsCompareOperandNull()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["n"] = null };
        var compiled = Conditional([Case("$.n", "gt", 1, "X")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("compare_operand_null", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    /// <summary>between の範囲が 2 要素でないとき between_operand_invalid になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_BetweenWithInvalidRange_RecordsBetweenOperandInvalid()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["n"] = 5 };
        var compiled = Conditional([Case("$.n", "between", new object[] { 1 }, "X")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("between_operand_invalid", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    /// <summary>空白 path は path_resolution_failed になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_EmptyPath_RecordsPathResolutionFailed()
    {
        // Arrange
        var compiled = Conditional([Case("   ", "exists", null, "X")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", new Dictionary<string, object?>(), onPathWarning: null);

        // Assert
        Assert.Equal("path_resolution_failed", diagnostics.CaseEvaluations[0].ReasonCode);
        Assert.Contains(diagnostics.EvaluationErrors, e => e.Contains("empty or whitespace", StringComparison.Ordinal));
    }

    /// <summary>path 警告コールバックが非対応 path で呼ばれることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_UnsupportedPath_InvokesOnPathWarning()
    {
        // Arrange
        var compiled = Conditional([Case("not-json-path", "exists", null, "X")]);
        var warnings = new List<(string Path, string Reason)>();

        // Act
        OutputConditionEvaluator.EvaluateDetailed(
            compiled,
            "Completed",
            new Dictionary<string, object?>(),
            (path, reason) => warnings.Add((path, reason)));

        // Assert
        Assert.Single(warnings);
        Assert.Equal("not-json-path", warnings[0].Path);
    }

    /// <summary>in で候補に含まれない値は condition_false になることを検証する。</summary>
    [Fact]
    public void EvaluateDetailed_InMiss_RecordsConditionFalse()
    {
        // Arrange
        var output = new Dictionary<string, object?> { ["v"] = "z" };
        var compiled = Conditional([Case("$.v", "in", new object[] { "a", "b" }, "Hit")]);

        // Act
        var (_, diagnostics) = OutputConditionEvaluator.EvaluateDetailed(
            compiled, "Completed", output, onPathWarning: null);

        // Assert
        Assert.Equal("condition_false", diagnostics.CaseEvaluations[0].ReasonCode);
    }

    private static CompiledFactTransition Conditional(
        IReadOnlyList<CompiledTransitionCase> cases,
        TransitionTarget? defaultTarget = null) =>
        new() { Cases = cases, DefaultTarget = defaultTarget };

    private static CompiledTransitionCase Case(
        string path,
        string op,
        object? value,
        string next) =>
        new()
        {
            Order = 0,
            DeclarationIndex = 0,
            When = new ConditionExpressionDefinition { Path = path, Op = op, Value = value },
            Target = new TransitionTarget { Next = next }
        };
}
