using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>
/// <see cref="SysPathCall"/> の構文 Theory。
/// </summary>
public class SysPathCallTests
{
    /// <summary>正当な CallPath を分解できることを検証する。</summary>
    [Theory]
    [InlineData("$.sys.now(\"yyyyMMdd\")", "now", "yyyyMMdd")]
    [InlineData("$.sys.utcNow(\"yyyy-MM-dd\")", "utcNow", "yyyy-MM-dd")]
    [InlineData("$.sys.now(\"yyyy-MM-dd'T'HH:mm:ss'Z'\")", "now", "yyyy-MM-dd'T'HH:mm:ss'Z'")]
    [InlineData("$.sys.now(\"x\")", "now", "x")]
    public void TryParse_ValidCallPath_ReturnsInfo(
        string path,
        string expectedFunctionName,
        string expectedFormat)
    {
        // Act
        var ok = SysPathCall.TryParse(path, out var info);

        // Assert
        Assert.True(ok);
        Assert.True(SysPathCall.IsValidCallPath(path));
        Assert.Equal(expectedFunctionName, info.FunctionName);
        Assert.Equal(expectedFormat, info.Argument);
    }

    /// <summary>許可ヒントがテーブルの引数プレースホルダから生成されることを検証する。</summary>
    [Fact]
    public void FormatAllowedCallPathsHint_IncludesTableFunctions()
    {
        // Act
        var hint = SysPathCall.FormatAllowedCallPathsHint();

        // Assert
        Assert.Contains("allowed call paths are", hint, StringComparison.Ordinal);
        Assert.Contains("$.sys.now(\"pattern\")", hint, StringComparison.Ordinal);
        Assert.Contains("$.sys.utcNow(\"pattern\")", hint, StringComparison.Ordinal);
        Assert.Contains(
            $"string argument length 1..{SysPathCall.MaxArgumentLength}",
            hint,
            StringComparison.Ordinal);
    }

    /// <summary>不正・未許可の CallPath を拒否することを検証する。</summary>
    [Theory]
    [InlineData("$.sys.now")]
    [InlineData("$.sys.utcNow")]
    [InlineData("$.sys.today")]
    [InlineData("$.sys.now()")]
    [InlineData("$.sys.now(yyyyMMdd)")]
    [InlineData("$.sys.now('yyyy')")]
    [InlineData("$.sys.today(\"yyyy\")")]
    [InlineData("$.sys.foo(\"x\")")]
    [InlineData("$.sys.now(\"\")")]
    [InlineData("$.sys.now(\"yyyyMMdd\").extra")]
    [InlineData("$.sys.now(\"yyyyMMdd\") ")]
    [InlineData(" $.sys.now(\"yyyyMMdd\")")]
    [InlineData("$.vars.now(\"yyyy\")")]
    [InlineData("")]
    public void TryParse_InvalidCallPath_ReturnsFalse(string path)
    {
        // Act
        var ok = SysPathCall.TryParse(path, out _);

        // Assert
        Assert.False(ok);
        Assert.False(SysPathCall.IsValidCallPath(path));
    }

    /// <summary>書式が最大長ちょうどのとき受理し、超過は拒否することを検証する。</summary>
    [Fact]
    public void TryParse_FormatLengthBoundary_EnforcesMaxFormatLength()
    {
        // Arrange
        var atMax = $"$.sys.now(\"{new string('y', SysPathCall.MaxArgumentLength)}\")";
        var overMax = $"$.sys.now(\"{new string('y', SysPathCall.MaxArgumentLength + 1)}\")";

        // Act / Assert
        Assert.True(SysPathCall.TryParse(atMax, out var info));
        Assert.Equal(SysPathCall.MaxArgumentLength, info.Argument.Length);
        Assert.False(SysPathCall.TryParse(overMax, out _));
    }

    /// <summary><c>$.sys.</c> かつ <c>(</c> を含むとき候補と判定することを検証する。</summary>
    [Theory]
    [InlineData("$.sys.now(\"yyyy\")", true)]
    [InlineData("$.sys.today(\"yyyy\")", true)]
    [InlineData("$.sys.now", false)]
    [InlineData("$.vars.x", false)]
    public void IsCallPathCandidate_DetectsSysParen(string path, bool expected)
    {
        // Act
        var candidate = SysPathCall.IsCallPathCandidate(path);

        // Assert
        Assert.Equal(expected, candidate);
    }
}
