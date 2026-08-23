using Statevia.Reference.Http;

namespace Statevia.Reference.Http.Tests;

/// <summary><see cref="HttpUrlValidator"/> の SSRF / HTTPS 検証。</summary>
public sealed class HttpUrlValidatorTests
{
    /// <summary>公開 HTTPS URL は許可される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_PublicHttps_DoesNotThrow()
    {
        // Arrange / Act
        var exception = Record.Exception(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://example.com/hook"));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>HTTP URL は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Http_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("http://example.com/hook"));
    }

    /// <summary>localhost は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Localhost_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://localhost/hook"));
    }

    /// <summary>プライベート IPv4 は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_PrivateIpv4_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://10.0.0.1/hook"));
    }

    /// <summary>ループバック IPv4 は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_LoopbackIpv4_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://127.0.0.1/hook"));
    }

    /// <summary>相対 URL は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Relative_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("/relative"));
    }

    /// <summary>.local / .internal ホストは拒否される。</summary>
    [Theory]
    [InlineData("https://svc.local/hook")]
    [InlineData("https://svc.internal/hook")]
    public void EnsureAllowedHttpsUrl_LocalSuffix_Throws(string url)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => HttpUrlValidator.EnsureAllowedHttpsUrl(url));
    }

    /// <summary>RFC1918 / link-local IPv4 は拒否される。</summary>
    [Theory]
    [InlineData("https://172.16.0.1/hook")]
    [InlineData("https://192.168.1.1/hook")]
    [InlineData("https://169.254.1.1/hook")]
    public void EnsureAllowedHttpsUrl_BlockedIpv4Ranges_Throws(string url)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => HttpUrlValidator.EnsureAllowedHttpsUrl(url));
    }

    /// <summary>IPv6 link-local は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Ipv6LinkLocal_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://[fe80::1]/hook"));
    }

    /// <summary>IPv6 site-local は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Ipv6SiteLocal_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://[fec0::1]/hook"));
    }

    /// <summary>IPv6 ループバックは拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_Ipv6Loopback_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://[::1]/hook"));
    }

    /// <summary>公開 IPv6 は許可される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_PublicIpv6_DoesNotThrow()
    {
        // Arrange / Act
        var exception = Record.Exception(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://[2001:4860:4860::8888]/hook"));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>ホストが空の HTTPS URL は拒否される。</summary>
    [Fact]
    public void EnsureAllowedHttpsUrl_EmptyHost_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrl("https://"));
    }
}
