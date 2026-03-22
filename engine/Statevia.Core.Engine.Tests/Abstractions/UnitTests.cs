using Statevia.Core.Engine.Abstractions;
using Xunit;

namespace Statevia.Core.Engine.Tests.Abstractions;

public class UnitTests
{
    /// <summary>Unit.Value が取得できることを検証する。</summary>
    [Fact]
    public void Value_IsAvailable()
    {
        // Arrange
        // （なし）

        // Act
        var u = Unit.Value;

        // Assert
        Assert.True(u.Equals(Unit.Value));
    }

    /// <summary>Unit 同士の Equals が常に true を返すことを検証する。</summary>
    [Fact]
    public void Equals_Unit_AlwaysReturnsTrue()
    {
        // Arrange
        var a = Unit.Value;
        var b = default(Unit);

        // Act
        var result = a.Equals(b);

        // Assert
        Assert.True(result);
    }

    /// <summary>Equals(object) が Unit なら true、それ以外なら false を返すことを検証する。</summary>
    [Fact]
    public void Equals_Object_ReturnsTrueOnlyForUnit()
    {
        // Arrange
        var u = Unit.Value;

        // Act
        var eqSelf = u.Equals((object)Unit.Value);
        var eqDefault = u.Equals((object)default(Unit));
        var eqNull = u.Equals((object?)null);
        var eqInt = u.Equals((object)42);
        var eqStr = u.Equals((object)"x");

        // Assert
        Assert.True(eqSelf);
        Assert.True(eqDefault);
        Assert.False(eqNull);
        Assert.False(eqInt);
        Assert.False(eqStr);
    }

    /// <summary>GetHashCode が常に 0 を返すことを検証する。</summary>
    [Fact]
    public void GetHashCode_AlwaysReturnsZero()
    {
        // Arrange
        var u = Unit.Value;

        // Act
        var hash = u.GetHashCode();

        // Assert
        Assert.Equal(0, hash);
    }

    /// <summary>ToString が "()" を返すことを検証する。</summary>
    [Fact]
    public void ToString_ReturnsParentheses()
    {
        // Arrange
        var u = Unit.Value;

        // Act
        var s = u.ToString();

        // Assert
        Assert.Equal("()", s);
    }

    /// <summary>等価演算子 == が常に true を返すことを検証する。</summary>
    [Fact]
    public void EqualityOperator_AlwaysReturnsTrue()
    {
        // Arrange
        var a = Unit.Value;
        var b = default(Unit);

        // Act
        var ab = a == b;
        var ba = b == a;

        // Assert
        Assert.True(ab);
        Assert.True(ba);
    }

    /// <summary>不等価演算子 != が常に false を返すことを検証する。</summary>
    [Fact]
    public void InequalityOperator_AlwaysReturnsFalse()
    {
        // Arrange
        var a = Unit.Value;
        var b = default(Unit);

        // Act
        var ab = a != b;
        var ba = b != a;

        // Assert
        Assert.False(ab);
        Assert.False(ba);
    }
}
