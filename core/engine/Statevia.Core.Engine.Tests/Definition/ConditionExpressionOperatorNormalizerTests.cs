using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

public class ConditionExpressionOperatorNormalizerTests
{
    /// <summary>空または空白の演算子は正規化できないことを検証する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_EmptyOrWhitespace_ReturnsFalse(string? raw)
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize(raw, out var canonical);

        // Assert
        Assert.False(ok);
        Assert.Equal(string.Empty, canonical);
    }

    /// <summary>記号 <c>=</c> と <c>==</c> が EQ に正規化されることを検証する。</summary>
    [Theory]
    [InlineData("=")]
    [InlineData("==")]
    public void TryNormalize_EqualitySymbols_MapToEq(string raw)
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize(raw, out var canonical);

        // Assert
        Assert.True(ok);
        Assert.Equal("EQ", canonical);
    }

    /// <summary>記号 <c>!=</c> と <c>&lt;&gt;</c> が NE に正規化されることを検証する。</summary>
    [Theory]
    [InlineData("!=")]
    [InlineData("<>")]
    public void TryNormalize_InequalitySymbols_MapToNe(string raw)
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize(raw, out var canonical);

        // Assert
        Assert.True(ok);
        Assert.Equal("NE", canonical);
    }

    /// <summary>大小比較の記号が対応する略号へ正規化されることを検証する。</summary>
    [Theory]
    [InlineData(">", "GT")]
    [InlineData(">=", "GTE")]
    [InlineData("<", "LT")]
    [InlineData("<=", "LTE")]
    public void TryNormalize_OrderingSymbols_MapToCanonical(string raw, string expected)
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize(raw, out var canonical);

        // Assert
        Assert.True(ok);
        Assert.Equal(expected, canonical);
    }

    /// <summary>小文字の既知演算子名が大文字略号へ正規化されることを検証する。</summary>
    [Fact]
    public void TryNormalize_LowerCaseWord_NormalizesToUpper()
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize("  eq  ", out var canonical);

        // Assert
        Assert.True(ok);
        Assert.Equal("EQ", canonical);
    }

    /// <summary>定義にない演算子は正規化できないことを検証する。</summary>
    [Fact]
    public void TryNormalize_UnknownOperator_ReturnsFalse()
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize("MATCHES_REGEX", out var canonical);

        // Assert
        Assert.False(ok);
        Assert.Equal(string.Empty, canonical);
    }

    /// <summary>EXISTS はそのまま正規形として受理されることを検証する。</summary>
    [Fact]
    public void TryNormalize_ExistsCanonical_Accepted()
    {
        // Act
        var ok = ConditionExpressionOperatorNormalizer.TryNormalize("EXISTS", out var canonical);

        // Assert
        Assert.True(ok);
        Assert.Equal("EXISTS", canonical);
    }
}
