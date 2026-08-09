namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary><see cref="DefaultIdGenerator"/> の Dual API 採番を検証する。</summary>
public sealed class DefaultIdGeneratorTests
{
    /// <summary>
    /// NewSequentialGuid は空値にならず、UUID version が 7 である。
    /// </summary>
    [Fact]
    public void NewSequentialGuid_ReturnsNonEmptyUuidV7()
    {
        // Arrange
        var sut = new DefaultIdGenerator();

        // Act
        var id = sut.NewSequentialGuid();

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(7, GetUuidVersion(id));
    }

    /// <summary>
    /// NewSequentialGuid の連続生成は同じ値にならない。
    /// </summary>
    [Fact]
    public void NewSequentialGuid_ReturnsDifferentValues()
    {
        // Arrange
        var sut = new DefaultIdGenerator();

        // Act
        var id1 = sut.NewSequentialGuid();
        var id2 = sut.NewSequentialGuid();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    /// <summary>
    /// NewRandomGuid は空値にならず、UUID version が 4 である。
    /// </summary>
    [Fact]
    public void NewRandomGuid_ReturnsNonEmptyUuidV4()
    {
        // Arrange
        var sut = new DefaultIdGenerator();

        // Act
        var id = sut.NewRandomGuid();

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(4, GetUuidVersion(id));
    }

    /// <summary>
    /// NewRandomGuid の連続生成は同じ値にならない。
    /// </summary>
    [Fact]
    public void NewRandomGuid_ReturnsDifferentValues()
    {
        // Arrange
        var sut = new DefaultIdGenerator();

        // Act
        var id1 = sut.NewRandomGuid();
        var id2 = sut.NewRandomGuid();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    /// <summary>UUID 文字列上の version nibble を返す（endian 非依存）。</summary>
    private static int GetUuidVersion(Guid guid)
    {
        // xxxxxxxx-xxxx-Mxxx-... の M（"N" 形式では index 12）
        var hex = guid.ToString("N");
        return int.Parse(
            hex.AsSpan(12, 1),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
