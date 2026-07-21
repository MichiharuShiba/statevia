using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="StateInputEvaluator"/> の path / values / 警告（Context 根）。</summary>
public class StateInputEvaluatorTests
{
    /// <summary>spec が null のとき候補 input がそのまま返ることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_NullSpec_ReturnsCandidateInput()
    {
        // Arrange
        var candidate = new Dictionary<string, object?> { ["x"] = 1 };
        var context = WorkflowExecutionContext.Create(null);

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(null, context, candidate);

        // Assert
        Assert.Same(candidate, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>$.input から開始 input を抽出できることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_InputPath_ResolvesWorkflowInput()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(new Dictionary<string, object?> { ["orderId"] = "ORD-1" });
        var spec = new StateInputDefinition { Path = "$.input.orderId" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);

        // Assert
        Assert.Equal("ORD-1", result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>$.states.&lt;Name&gt;.output から完了 State の output を抽出できることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_StatesOutputPath_ResolvesCompletedState()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetStateOutput("Fetch", new Dictionary<string, object?> { ["statusCode"] = 200 });
        var spec = new StateInputDefinition { Path = "$.states.Fetch.output.statusCode" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);

        // Assert
        Assert.Equal(200, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>未完了 State 参照は null と IncompleteStateOutput 警告になることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_IncompleteState_ReturnsNullWithWarning()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        var spec = new StateInputDefinition { Path = "$.states.Missing.output" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: "candidate");

        // Assert
        Assert.Null(result.Value);
        Assert.Single(result.Warnings);
        Assert.Equal(ExecutionContextPathResolver.IncompleteStateOutput, result.Warnings[0].Reason);
    }


    /// <summary>ドット付き State 名をブラケット記法で参照できることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_BracketDottedStateName_ResolvesOutput()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetStateOutput("order.notify.customer", new Dictionary<string, object?> { ["sent"] = true });
        var spec = new StateInputDefinition { Path = "$.states['order.notify.customer'].output.sent" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);

        // Assert
        Assert.Equal(true, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>ドット付き未完了 State のブラケット参照は IncompleteStateOutput になることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_BracketIncompleteDottedState_ReturnsNullWithWarning()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        var spec = new StateInputDefinition { Path = "$.states['order.notify.customer'].output" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);

        // Assert
        Assert.Null(result.Value);
        Assert.Single(result.Warnings);
        Assert.Equal(ExecutionContextPathResolver.IncompleteStateOutput, result.Warnings[0].Reason);
    }

    /// <summary>path が \"$\" のとき Context 全体が返ることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_RootPath_ReturnsContextRoot()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(new Dictionary<string, object?> { ["x"] = 1 });
        var spec = new StateInputDefinition { Path = "$" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: "ignored");
        var root = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Value);

        // Assert
        Assert.True(root.ContainsKey(ExecutionContextKeys.Input));
        Assert.True(root.ContainsKey(ExecutionContextKeys.States));
        Assert.True(root.ContainsKey(ExecutionContextKeys.Vars));
        Assert.True(root.ContainsKey(ExecutionContextKeys.Sys));
        Assert.Empty(result.Warnings);
    }

    /// <summary>values で path と literal を組み合わせ、ドットキーでネストできることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_Values_BuildsNestedDictionary()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        context.SetStateOutput(
            "A",
            new Dictionary<string, object?>
            {
                ["outer"] = new Dictionary<string, object?> { ["inner"] = "from-path" }
            });
        var spec = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["a.b"] = new() { Path = "$.states.A.output.outer.inner" },
                ["flag"] = new() { Literal = true }
            }
        };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);
        var dict = Assert.IsType<Dictionary<string, object?>>(result.Value);

        // Assert
        var nested = Assert.IsType<Dictionary<string, object?>>(dict["a"]);
        Assert.Equal("from-path", nested["b"]);
        Assert.True(Assert.IsType<bool>(dict["flag"]));
        Assert.Empty(result.Warnings);
    }

    /// <summary>非対応 path では警告が付き null が返る（raw フォールバックなし）ことを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_UnsupportedPath_AddsWarningAndReturnsNull()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        var spec = new StateInputDefinition { Path = "not-dollar-dot" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: new { x = 1 });

        // Assert
        Assert.Null(result.Value);
        Assert.Single(result.Warnings);
        Assert.Equal(SimpleJsonPathResolver.IgnoredNonDollarDotPath, result.Warnings[0].Reason);
    }

    /// <summary>values が空のとき候補 input がそのまま返ることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_EmptyValues_ReturnsCandidateInput()
    {
        // Arrange
        var candidate = new Dictionary<string, object?> { ["x"] = 1 };
        var context = WorkflowExecutionContext.Create(null);
        var spec = new StateInputDefinition { Values = new Dictionary<string, StateInputValueDefinition>() };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidate);

        // Assert
        Assert.Same(candidate, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary><see cref="StateInputEvaluator.Apply"/> が診断付き評価の値を返すことを検証する。</summary>
    [Fact]
    public void Apply_ReturnsValueFromDiagnostics()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(new Dictionary<string, object?> { ["n"] = 7 });
        var spec = new StateInputDefinition { Path = "$.input.n" };

        // Act
        var value = StateInputEvaluator.Apply(spec, context, candidateInput: null);

        // Assert
        Assert.Equal(7, value);
    }

    /// <summary>空のドットキーは結果辞書へ書き込まれないことを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_EmptyDottedKey_IsIgnored()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        var spec = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["."] = new() { Literal = "ignored" },
                ["ok"] = new() { Literal = 1 }
            }
        };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);
        var dict = Assert.IsType<Dictionary<string, object?>>(result.Value);

        // Assert
        Assert.Equal(1, dict["ok"]);
        Assert.False(dict.ContainsKey(""));
    }

    /// <summary>同一キー・理由の警告は重複抑制されることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_DuplicateWarnings_AreDeduped()
    {
        // Arrange
        var context = WorkflowExecutionContext.Create(null);
        var spec = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["k1"] = new() { Path = "bad1" },
                ["k2"] = new() { Path = "bad1" }
            }
        };

        // Act — 同一 path 文字列なら inputKey ごとに別警告（k1/k2）
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, context, candidateInput: null);

        // Assert
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, w =>
            Assert.Equal(SimpleJsonPathResolver.IgnoredNonDollarDotPath, w.Reason));
    }
}
