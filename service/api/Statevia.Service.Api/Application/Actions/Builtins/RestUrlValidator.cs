using System.Net;
using System.Net.Sockets;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>rest action の URL 検証（HTTPS 制約・SSRF 拒否）。</summary>
internal static class RestUrlValidator
{
    /// <summary>HTTPS の公開到達可能 URL か検証する。</summary>
    /// <param name="url">検証対象 URL。</param>
    /// <exception cref="ArgumentException">非 HTTPS または拒否対象ホストのとき。</exception>
    public static void EnsureAllowedHttpsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("rest action requires an absolute URL.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("rest action requires an HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("rest action URL host is required.");
        }

        if (IsBlockedHost(uri.Host))
        {
            throw new ArgumentException("rest action URL host is not allowed.");
        }

        if (IPAddress.TryParse(uri.Host, out var literalAddress) && IsBlockedAddress(literalAddress))
        {
            throw new ArgumentException("rest action URL host is not allowed.");
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
