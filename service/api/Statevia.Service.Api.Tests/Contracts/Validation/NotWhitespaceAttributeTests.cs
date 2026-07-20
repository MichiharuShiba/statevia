using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary><see cref="NotWhitespaceAttribute"/> の検証シナリオ。</summary>
public sealed class NotWhitespaceAttributeTests
{
    /// <summary>null は必須属性に委譲するため成功扱い。</summary>
    [Fact]
    public void IsValid_WhenNull_ReturnsTrue()
    {
        // Arrange
        var attribute = new NotWhitespaceAttribute();

        // Act
        var valid = attribute.IsValid(null);

        // Assert
        Assert.True(valid);
    }

    /// <summary>非空白文字列は成功する。</summary>
    [Theory]
    [InlineData("a")]
    [InlineData(" name ")]
    public void IsValid_WhenNonWhitespace_ReturnsTrue(string value)
    {
        // Arrange
        var attribute = new NotWhitespaceAttribute();

        // Act
        var valid = attribute.IsValid(value);

        // Assert
        Assert.True(valid);
    }

    /// <summary>空文字・空白のみは失敗する。</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void IsValid_WhenWhitespaceOnly_ReturnsFalse(string value)
    {
        // Arrange
        var attribute = new NotWhitespaceAttribute();

        // Act
        var valid = attribute.IsValid(value);

        // Assert
        Assert.False(valid);
    }

    /// <summary>string 以外は対象外として成功する。</summary>
    [Fact]
    public void IsValid_WhenNonString_ReturnsTrue()
    {
        // Arrange
        var attribute = new NotWhitespaceAttribute();

        // Act
        var valid = attribute.IsValid(42);

        // Assert
        Assert.True(valid);
    }
}
