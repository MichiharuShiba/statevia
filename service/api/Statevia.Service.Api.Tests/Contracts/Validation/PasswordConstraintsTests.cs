using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary><see cref="PasswordConstraints"/> の文字種・長さ。</summary>
public sealed class PasswordConstraintsTests
{
    /// <summary>8 文字以上なら記号・大文字小文字混在なしでも有効。</summary>
    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("Pass1234")]
    [InlineData("12345678")]
    [InlineData("aaaaaaaa")]
    [InlineData("pass-word")]
    [InlineData("pass_word")]
    [InlineData("P@ssw0rd!")]
    public void IsValid_WhenNonWhitespaceAtLeast8_ReturnsTrue(string password)
    {
        // Arrange & Act
        var valid = PasswordConstraints.IsValid(password);

        // Assert
        Assert.True(valid);
    }

    /// <summary>短すぎ・空白を含む値は無効。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("pass word")]
    public void IsValid_WhenDisallowed_ReturnsFalse(string password)
    {
        // Arrange & Act
        var valid = PasswordConstraints.IsValid(password);

        // Assert
        Assert.False(valid);
    }

    /// <summary>8 と 128 は有効、7 と 129 は無効。</summary>
    [Fact]
    public void IsValid_WhenLengthBoundary_Enforces8To128()
    {
        // Arrange
        var atMin = new string('a', PasswordConstraints.MinLength);
        var belowMin = new string('a', PasswordConstraints.MinLength - 1);
        var atMax = new string('a', PasswordConstraints.MaxLength);
        var aboveMax = new string('a', PasswordConstraints.MaxLength + 1);

        // Act & Assert
        Assert.True(PasswordConstraints.IsValid(atMin));
        Assert.False(PasswordConstraints.IsValid(belowMin));
        Assert.True(PasswordConstraints.IsValid(atMax));
        Assert.False(PasswordConstraints.IsValid(aboveMax));
    }
}
