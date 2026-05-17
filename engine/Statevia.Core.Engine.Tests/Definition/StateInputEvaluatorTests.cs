using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="StateInputEvaluator"/> の path / values / 警告。</summary>
public class StateInputEvaluatorTests
{
    /// <summary>spec が null のとき raw がそのまま返ることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_NullSpec_ReturnsRawInput()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["x"] = 1 };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(null, raw);

        // Assert
        Assert.Same(raw, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>トップレベル path でネスト値を抽出できることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_TopLevelPath_ExtractsValue()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["score"] = 42 };
        var spec = new StateInputDefinition { Path = "$.score" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);

        // Assert
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>values で path と literal を組み合わせ、ドットキーでネストできることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_Values_BuildsNestedDictionary()
    {
        // Arrange
        var raw = new Dictionary<string, object?>
        {
            ["outer"] = new Dictionary<string, object?> { ["inner"] = "from-path" }
        };
        var spec = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["a.b"] = new() { Path = "$.outer.inner" },
                ["flag"] = new() { Literal = true }
            }
        };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);
        var dict = Assert.IsType<Dictionary<string, object?>>(result.Value);

        // Assert
        var nested = Assert.IsType<Dictionary<string, object?>>(dict["a"]);
        Assert.Equal("from-path", nested["b"]);
        Assert.True(Assert.IsType<bool>(dict["flag"]));
        Assert.Empty(result.Warnings);
    }

    /// <summary>非対応 path では警告が付き raw がフォールバックされることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_UnsupportedPath_AddsWarningAndReturnsRaw()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["x"] = 1 };
        var spec = new StateInputDefinition { Path = "not-dollar-dot" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);

        // Assert
        Assert.Same(raw, result.Value);
        Assert.Single(result.Warnings);
        Assert.Equal(SimpleJsonPathResolver.IgnoredNonDollarDotPath, result.Warnings[0].Reason);
    }

    /// <summary>values が空のとき raw がそのまま返ることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_EmptyValues_ReturnsRawInput()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["x"] = 1 };
        var spec = new StateInputDefinition { Values = new Dictionary<string, StateInputValueDefinition>() };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);

        // Assert
        Assert.Same(raw, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary>path が "$" のとき raw がそのまま返ることを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_RootPath_ReturnsRawInput()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["x"] = 1 };
        var spec = new StateInputDefinition { Path = "$" };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);

        // Assert
        Assert.Same(raw, result.Value);
        Assert.Empty(result.Warnings);
    }

    /// <summary><see cref="StateInputEvaluator.Apply"/> が診断付き評価の値を返すことを検証する。</summary>
    [Fact]
    public void Apply_ReturnsValueFromDiagnostics()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["n"] = 7 };
        var spec = new StateInputDefinition { Path = "$.n" };

        // Act
        var value = StateInputEvaluator.Apply(spec, raw);

        // Assert
        Assert.Equal(7, value);
    }

    /// <summary>空のドットキーは結果辞書へ書き込まれないことを検証する。</summary>
    [Fact]
    public void ApplyWithDiagnostics_EmptyDottedKey_IsIgnored()
    {
        // Arrange
        var raw = new Dictionary<string, object?> { ["x"] = 1 };
        var spec = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["."] = new() { Literal = "ignored" },
                ["ok"] = new() { Literal = 1 }
            }
        };

        // Act
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);
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
        var raw = new Dictionary<string, object?> { ["x"] = 1 };
        var spec = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["k1"] = new() { Path = "bad1" },
                ["k2"] = new() { Path = "bad1" }
            }
        };

        // Act — 同一 path 文字列なら inputKey ごとに別警告（k1/k2）
        var result = StateInputEvaluator.ApplyWithDiagnostics(spec, raw);

        // Assert
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, w =>
            Assert.Equal(SimpleJsonPathResolver.IgnoredNonDollarDotPath, w.Reason));
    }
}
