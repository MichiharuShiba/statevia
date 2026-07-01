using Statevia.Service.Api.Application.Actions.Builtins;

namespace Statevia.Service.Api.Tests.Application.Actions.Builtins;

/// <summary><see cref="RestUrlValidator"/> の SSRF / HTTPS 検証テスト。</summary>
public sealed class RestUrlValidatorTests
{
    /// <summary>公開 HTTPS URL は許可される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_PublicHttps_DoesNotThrow()
    {
        // Act / Assert
        var exception = Record.Exception(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("https://example.com/hook"));
        Assert.Null(exception);
    }

    /// <summary>HTTP URL は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Http_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("http://example.com/hook"));
    }

    /// <summary>localhost は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Localhost_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("https://localhost/hook"));
    }

    /// <summary>プライベート IPv4 は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_PrivateIpv4_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("https://10.0.0.1/hook"));
    }

    /// <summary>ループバック IPv4 は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_LoopbackIpv4_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("https://127.0.0.1/hook"));
    }
}
