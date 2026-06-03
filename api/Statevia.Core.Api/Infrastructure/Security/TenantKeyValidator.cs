using System.Text.RegularExpressions;

namespace Statevia.Core.Api.Infrastructure.Security;

/// <summary>外部テナントキー（<c>tenant_key</c>）の検証。</summary>
internal static partial class TenantKeyValidator
{
    private const int MaxLength = 64;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex TenantKeyPattern();

    /// <summary>テナントキーを正規化し、形式を検証する。</summary>
    /// <param name="tenantKey">入力キー。</param>
    /// <returns>トリム済みキー。</returns>
    public static string Normalize(string tenantKey)
    {
        var normalized = tenantKey.Trim();
        if (normalized.Length is 0 or > MaxLength)
        {
            throw new ArgumentException(
                $"tenantKey must be 1–{MaxLength} characters.",
                nameof(tenantKey));
        }

        if (!TenantKeyPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "tenantKey must use lowercase letters, digits, hyphens, and dots (no leading/trailing hyphen or dot).",
                nameof(tenantKey));
        }

        return normalized;
    }
}
