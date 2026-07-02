using System.Security.Cryptography;
using System.Text;

namespace Statevia.Core.Application.Contracts.Security;

/// <summary><see cref="ExecutionSecuritySnapshot.PermissionSetHash"/> の算出。</summary>
public static class PermissionSetHash
{
    /// <summary>
    /// permission key 集合から SHA-256（小文字 hex）を算出する。
    /// </summary>
    /// <param name="permissionKeys">展開済み semantic permission key。</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "docs/execution-security-snapshot.md で小文字 hex を規定。")]
    public static string Compute(IEnumerable<string> permissionKeys)
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);

        var normalized = permissionKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
            return Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant();

        var payload = string.Join('\n', normalized);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
