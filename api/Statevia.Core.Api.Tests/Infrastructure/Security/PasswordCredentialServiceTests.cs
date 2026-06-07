using Statevia.Core.Api.Infrastructure.Security;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary><see cref="PasswordCredentialService"/> のハッシュ・検証。</summary>
public sealed class PasswordCredentialServiceTests
{
    /// <summary>ハッシュ化したパスワードは正しい平文で検証できる。</summary>
    [Fact]
    public void HashPassword_AndVerifyPassword_MatchForCorrectPlaintext()
    {
        // Arrange
        var service = new PasswordCredentialService();
        const string password = "secret-password";

        // Act
        var hash = service.HashPassword(password);

        // Assert
        Assert.True(service.VerifyPassword(password, hash));
        Assert.False(service.VerifyPassword("wrong", hash));
    }

    /// <summary>API キーのハッシュと prefix が決定的に生成される。</summary>
    [Fact]
    public void HashApiKey_AndApiKeyPrefix_ReturnExpectedValues()
    {
        // Arrange
        const string plainKey = "sk_live_abcdefghijklmnop";

        // Act
        var hash = PasswordCredentialService.HashApiKey(plainKey);
        var prefix = PasswordCredentialService.ApiKeyPrefix(plainKey);
        var shortPrefix = PasswordCredentialService.ApiKeyPrefix("abc");

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.Equal(PasswordCredentialService.HashApiKey(plainKey), hash);
        Assert.Equal("sk_live_", prefix);
        Assert.Equal("abc", shortPrefix);
    }

    /// <summary>生成した API キーは stv_ プレフィックスを持つ。</summary>
    [Fact]
    public void GeneratePlainApiKey_ReturnsPrefixedUniqueValues()
    {
        // Act
        var first = PasswordCredentialService.GeneratePlainApiKey();
        var second = PasswordCredentialService.GeneratePlainApiKey();

        // Assert
        Assert.StartsWith("stv_", first, StringComparison.Ordinal);
        Assert.StartsWith("stv_", second, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }
}
