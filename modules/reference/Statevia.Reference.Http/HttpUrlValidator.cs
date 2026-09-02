using System.Net;
using System.Net.Sockets;

namespace Statevia.Reference.Http;

/// <summary>HTTP リファレンス Action の URL 検証（HTTPS 制約・SSRF 拒否）。</summary>
/// <remarks>
/// <para>リテラルの loopback / プライベート IP / localhost / .local / .internal を拒否する。</para>
/// <para>ホスト名は都度 DNS 解決し、返った全 A/AAAA を同じ拒否規則で見る。失敗・タイムアウト（2 秒）は拒否。結果はキャッシュしない。</para>
/// <para>利用者が Module ソースを改変すれば外せることは意図どおりである。</para>
/// </remarks>
public static class HttpUrlValidator
{
    /// <summary>ホスト名解決の打ち切り時間。リバインド待ちを無限にしないため 2 秒とする。</summary>
    internal static readonly TimeSpan DnsResolveTimeout = TimeSpan.FromSeconds(2);

    private const string HostNotAllowedMessage = "http.request action URL host is not allowed.";

    /// <summary>HTTPS の公開到達可能 URL か検証する。</summary>
    /// <param name="url">検証対象 URL。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <exception cref="ArgumentException">非 HTTPS、拒否対象ホスト、DNS 失敗・タイムアウトのとき。</exception>
    public static Task EnsureAllowedHttpsUrlAsync(string url, CancellationToken cancellationToken = default) =>
        EnsureAllowedHttpsUrlAsync(url, ResolveDnsAsync, cancellationToken);

    /// <summary>DNS 解決を差し替えて HTTPS URL を検証する。</summary>
    /// <param name="url">検証対象 URL。</param>
    /// <param name="resolveAddressesAsync">ホスト名の A/AAAA 解決。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    internal static async Task EnsureAllowedHttpsUrlAsync(
        string url,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolveAddressesAsync);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("http.request action requires an absolute URL.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("http.request action requires an HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("http.request action URL host is required.");
        }

        if (IsBlockedHost(uri.Host))
        {
            throw new ArgumentException(HostNotAllowedMessage);
        }

        if (IPAddress.TryParse(uri.Host, out var literalAddress))
        {
            if (IsBlockedAddress(NormalizeAddress(literalAddress)))
            {
                throw new ArgumentException(HostNotAllowedMessage);
            }

            return;
        }

        var addresses = await ResolveAddressesAsync(uri.Host, resolveAddressesAsync, cancellationToken)
            .ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(address => IsBlockedAddress(NormalizeAddress(address))))
        {
            throw new ArgumentException(HostNotAllowedMessage);
        }
    }

    /// <summary>ホスト名をシステム DNS で解決する。</summary>
    internal static Task<IPAddress[]> ResolveDnsAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);

    private static async Task<IPAddress[]> ResolveAddressesAsync(
        string host,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DnsResolveTimeout);
        try
        {
            return await resolveAddressesAsync(host, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ArgumentException(HostNotAllowedMessage);
        }
        catch (SocketException)
        {
            throw new ArgumentException(HostNotAllowedMessage);
        }
    }

    private static bool IsBlockedHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
    }

    private static IPAddress NormalizeAddress(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 127;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        return false;
    }
}
