using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary><see cref="UsernameConstraints"/> の文字種・長さ。</summary>
public sealed class UsernameConstraintsTests
{
    /// <summary>許可文字は有効。</summary>
    [Theory]
    [InlineData("a")]
    [InlineData("Admin_1")]
    [InlineData("ops.user-2")]
    [InlineData("A.Z_z-0")]
    public void IsValid_WhenAllowedCharset_ReturnsTrue(string username)
    {
        // Arrange & Act
        var valid = UsernameConstraints.IsValid(username);

        // Assert
        Assert.True(valid);
    }

    /// <summary>空・長すぎ・禁止文字は無効。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("user@example.com")]
    [InlineData("user name")]
    [InlineData("user+tag")]
    [InlineData("ユーザー")]
    [InlineData("-ops")]
    [InlineData("ops-")]
    [InlineData(".ops")]
    [InlineData("ops.")]
    [InlineData("_ops")]
    [InlineData("ops_")]
    public void IsValid_WhenDisallowed_ReturnsFalse(string username)
    {
        // Arrange & Act
        var valid = UsernameConstraints.IsValid(username);

        // Assert
        Assert.False(valid);
    }

    /// <summary>64 文字は有効、65 文字は無効。</summary>
    [Fact]
    public void IsValid_WhenLengthBoundary_Enforces64()
    {
        // Arrange
        var atLimit = new string('a', UsernameConstraints.MaxLength);
        var tooLong = new string('a', UsernameConstraints.MaxLength + 1);

        // Act & Assert
        Assert.True(UsernameConstraints.IsValid(atLimit));
        Assert.False(UsernameConstraints.IsValid(tooLong));
    }
}
