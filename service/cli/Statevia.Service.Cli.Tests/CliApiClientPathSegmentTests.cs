using Statevia.Service.Cli.Infrastructure;
using Xunit;

namespace Statevia.Service.Cli.Tests;

/// <summary>URI パス 1 セグメント検証の単体テスト。</summary>
public sealed class CliApiClientPathSegmentTests
{
    /// <summary>通常の実行 ID・ノード ID はエンコードして通る。</summary>
    [Theory]
    [InlineData("ex-1", "ex-1")]
    [InlineData("abc123def456", "abc123def456")]
    [InlineData("  wait_1  ", "wait_1")]
    [InlineData("my.definition", "my.definition")]
    public void TryEncodePathSegment_AcceptsSingleSegment(string value, string expected)
    {
        // Act
        var ok = CliApiClient.TryEncodePathSegment(value, out var encoded, out var error);

        // Assert
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expected, encoded);
    }

    /// <summary>スラッシュ・ドットセグメント・制御文字は拒否する。</summary>
    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("ab\nc")]
    public void TryEncodePathSegment_RejectsUnsafeSegment(string value)
    {
        // Act
        var ok = CliApiClient.TryEncodePathSegment(value, out var encoded, out var error);

        // Assert
        Assert.False(ok);
        Assert.Equal(string.Empty, encoded);
        Assert.NotNull(error);
        Assert.Contains("path segment", error, StringComparison.Ordinal);
    }

    /// <summary>空白のみは必須エラーにする。</summary>
    [Fact]
    public void TryEncodePathSegment_Whitespace_IsRequired()
    {
        // Act
        var ok = CliApiClient.TryEncodePathSegment("   ", out _, out var error);

        // Assert
        Assert.False(ok);
        Assert.Equal("is required.", error);
    }
}
