using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="TenantKeyValidator"/> の検証。</summary>
public sealed class TenantKeyValidatorTests
{
    /// <summary>有効なキーを正規化する。</summary>
    [Theory]
    [InlineData("default", "default")]
    [InlineData(" acme-corp ", "acme-corp")]
    [InlineData("a1", "a1")]
    [InlineData("statevia.dev", "statevia.dev")]
    public void Normalize_ValidKey_ReturnsTrimmed(string input, string expected)
    {
        // Act
        var result = TenantKeyValidator.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>不正なキーは例外。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Acme")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData(".acme")]
    [InlineData("acme.")]
    public void Normalize_InvalidKey_Throws(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => TenantKeyValidator.Normalize(input));
    }
}
