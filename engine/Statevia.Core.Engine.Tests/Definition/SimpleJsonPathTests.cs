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
}
