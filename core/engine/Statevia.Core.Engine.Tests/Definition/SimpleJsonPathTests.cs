using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

public class SimpleJsonPathTests
{
    /// <summary>パスが <c>$</c> のとき <see cref="SimpleJsonPath.IsValid"/> が true を返すことを検証する。</summary>
    [Fact]
    public void IsValid_RootOnly_ReturnsTrue()
    {
        // Arrange
        const string path = "$";

        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.True(ok);
    }

    /// <summary>ドット区切りの英数字セグメントのみのパスで true になることを検証する。</summary>
    [Fact]
    public void IsValid_DottedSegments_ReturnsTrue()
    {
        // Arrange
        const string path = "$.a.b_c2";

        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.True(ok);
    }

    /// <summary><c>$.</c> で始まらないパスは無効であることを検証する。</summary>
    [Fact]
    public void IsValid_NotStartingWithDollarDot_ReturnsFalse()
    {
        // Arrange
        const string path = "a.b";

        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.False(ok);
    }

    /// <summary>末尾が <c>.</c> のパスは無効であることを検証する。</summary>
    [Fact]
    public void IsValid_TrailingDot_ReturnsFalse()
    {
        // Arrange
        const string path = "$.a.";

        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.False(ok);
    }

    /// <summary><c>$.</c> のみ（セグメント無し）は無効であることを検証する。</summary>
    [Fact]
    public void IsValid_DollarDotOnly_ReturnsFalse()
    {
        // Arrange
        const string path = "$.";

        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.False(ok);
    }

    /// <summary>セグメントに英数字とアンダースコア以外が含まれると無効であることを検証する。</summary>
    [Fact]
    public void IsValid_InvalidCharacterInSegment_ReturnsFalse()
    {
        // Arrange
        const string path = "$.a-b";

        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.False(ok);
    }


    /// <summary>ブラケット＋引用でドット付きキーを含むパスが有効であることを検証する。</summary>
    [Theory]
    [InlineData("$.states['order.notify.customer'].output")]
    [InlineData("$.states[\"order.notify.customer\"].output")]
    [InlineData("$['input'].orderId")]
    public void IsValid_BracketQuotedSegment_ReturnsTrue(string path)
    {
        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.True(ok);
    }

    /// <summary>閉じられていないブラケットや空キーは無効であることを検証する。</summary>
    [Theory]
    [InlineData("$.states['order.notify.customer")]
    [InlineData("$.states[''].output")]
    [InlineData("$.states[order.notify.customer].output")]
    public void IsValid_InvalidBracket_ReturnsFalse(string path)
    {
        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.False(ok);
    }

    /// <summary>配列インデックス付きパスが有効であることを検証する。</summary>
    [Theory]
    [InlineData("$.vars.users[0]")]
    [InlineData("$.vars.users[0].email")]
    [InlineData("$.input.order.items[0]")]
    [InlineData("$[0]")]
    [InlineData("$.a[12].b")]
    public void IsValid_ArrayIndex_ReturnsTrue(string path)
    {
        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.True(ok);
    }

    /// <summary>未サポートのインデックス構文が無効であることを検証する。</summary>
    [Theory]
    [InlineData("$.users[*]")]
    [InlineData("$.users[-1]")]
    [InlineData("$.users[1:3]")]
    [InlineData("$.users[?(@.x)]")]
    [InlineData("$.users[ 0 ]")]
    [InlineData("$.users[01]")]
    [InlineData("$.users[]")]
    public void IsValid_UnsupportedIndexSyntax_ReturnsFalse(string path)
    {
        // Act
        var ok = SimpleJsonPath.IsValid(path);

        // Assert
        Assert.False(ok);
    }

    /// <summary>TryGetSegments が ArrayIndex と QuotedKey を区別することを検証する。</summary>
    [Fact]
    public void TryGetSegments_ArrayIndexAndQuotedZero_AreDistinctKinds()
    {
        // Arrange / Act
        var indexOk = SimpleJsonPath.TryGetSegments("$.items[0]", out var indexSegments);
        var keyOk = SimpleJsonPath.TryGetSegments("$.items['0']", out var keySegments);

        // Assert
        Assert.True(indexOk);
        Assert.True(keyOk);
        Assert.Equal(PathSegmentKind.ArrayIndex, indexSegments[1].Kind);
        Assert.Equal(0, indexSegments[1].Index);
        Assert.Equal(PathSegmentKind.QuotedKey, keySegments[1].Kind);
        Assert.Equal("0", keySegments[1].Name);
    }
}
