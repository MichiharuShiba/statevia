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

    /// <summary>相対 URL は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Relative_Throws()
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("/relative"));
    }

    /// <summary>.local / .internal ホストは拒否される。</summary>
    [Theory]
    [InlineData("https://svc.local/hook")]
    [InlineData("https://svc.internal/hook")]
    public void EnsureAllowedHttpsUrl_LocalSuffix_Throws(string url)
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(() => RestUrlValidator.EnsureAllowedHttpsUrl(url));
    }

    /// <summary>RFC1918 / link-local IPv4 は拒否される。</summary>
    [Theory]
    [InlineData("https://172.16.0.1/hook")]
    [InlineData("https://192.168.1.1/hook")]
    [InlineData("https://169.254.1.1/hook")]
    public void EnsureAllowedHttpsUrl_BlockedIpv4Ranges_Throws(string url)
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(() => RestUrlValidator.EnsureAllowedHttpsUrl(url));
    }

    /// <summary>IPv6 link-local は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Ipv6LinkLocal_Throws()
    {
        // Act + Assert
        Assert.Throws<ArgumentException>(() =>
            RestUrlValidator.EnsureAllowedHttpsUrl("https://[fe80::1]/hook"));
    }
}
