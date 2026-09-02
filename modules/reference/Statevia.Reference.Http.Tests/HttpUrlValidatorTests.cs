using System.Net;
using System.Net.Sockets;

namespace Statevia.Reference.Http.Tests;

/// <summary><see cref="HttpUrlValidator"/> の SSRF / HTTPS 検証。</summary>
public sealed class HttpUrlValidatorTests
{
    /// <summary>公開 HTTPS URL は解決後アドレスが公開なら許可される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_PublicHttps_DoesNotThrow()
    {
        // Arrange / Act
        var exception = await Record.ExceptionAsync(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(
                "https://example.com/hook",
                ResolvePublic,
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>HTTP URL は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_Http_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("http://example.com/hook", CancellationToken.None));
    }

    /// <summary>localhost は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_Localhost_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://localhost/hook", CancellationToken.None));
    }

    /// <summary>プライベート IPv4 は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_PrivateIpv4_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://10.0.0.1/hook", CancellationToken.None));
    }

    /// <summary>ループバック IPv4 は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_LoopbackIpv4_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://127.0.0.1/hook", CancellationToken.None));
    }

    /// <summary>相対 URL は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_Relative_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("/relative", CancellationToken.None));
    }

    /// <summary>.local / .internal ホストは拒否される。</summary>
    [Theory]
    [InlineData("https://svc.local/hook")]
    [InlineData("https://svc.internal/hook")]
    public async Task EnsureAllowedHttpsUrlAsync_LocalSuffix_Throws(string url)
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(url, CancellationToken.None));
    }

    /// <summary>RFC1918 / link-local IPv4 は拒否される。</summary>
    [Theory]
    [InlineData("https://172.16.0.1/hook")]
    [InlineData("https://192.168.1.1/hook")]
    [InlineData("https://169.254.1.1/hook")]
    public async Task EnsureAllowedHttpsUrlAsync_BlockedIpv4Ranges_Throws(string url)
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(url, CancellationToken.None));
    }

    /// <summary>IPv6 link-local は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_Ipv6LinkLocal_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://[fe80::1]/hook", CancellationToken.None));
    }

    /// <summary>IPv6 site-local は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_Ipv6SiteLocal_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://[fec0::1]/hook", CancellationToken.None));
    }

    /// <summary>IPv6 ループバックは拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_Ipv6Loopback_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://[::1]/hook", CancellationToken.None));
    }

    /// <summary>公開 IPv6 は許可される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_PublicIpv6_DoesNotThrow()
    {
        // Arrange / Act
        var exception = await Record.ExceptionAsync(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://[2001:4860:4860::8888]/hook", CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>ホストが空の HTTPS URL は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_EmptyHost_Throws()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync("https://", CancellationToken.None));
    }

    /// <summary>解決結果にプライベート IP が混ざると拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_ResolvedPrivateAddress_Throws()
    {
        // Arrange
        Task<IPAddress[]> Resolve(string _, CancellationToken __) =>
            Task.FromResult(new[]
            {
                IPAddress.Parse("93.184.216.34"),
                IPAddress.Parse("10.0.0.8"),
            });

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(
                "https://rebind.example/hook",
                Resolve,
                CancellationToken.None));
    }

    /// <summary>解決失敗は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_DnsFailure_Throws()
    {
        // Arrange
        static Task<IPAddress[]> Fail(string _, CancellationToken __) =>
            Task.FromException<IPAddress[]>(new SocketException());

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(
                "https://missing.example/hook",
                Fail,
                CancellationToken.None));
    }

    /// <summary>空の解決結果は拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_EmptyResolve_Throws()
    {
        // Arrange
        static Task<IPAddress[]> Empty(string _, CancellationToken __) =>
            Task.FromResult(Array.Empty<IPAddress>());

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(
                "https://empty.example/hook",
                Empty,
                CancellationToken.None));
    }

    /// <summary>DNS タイムアウトは拒否される。</summary>
    [Fact]
    public async Task EnsureAllowedHttpsUrlAsync_DnsTimeout_Throws()
    {
        // Arrange
        static async Task<IPAddress[]> Hang(string _, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return [];
        }

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HttpUrlValidator.EnsureAllowedHttpsUrlAsync(
                "https://slow.example/hook",
                Hang,
                CancellationToken.None));
    }

    private static Task<IPAddress[]> ResolvePublic(string _, CancellationToken __) =>
        Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
}
