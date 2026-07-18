using System.Text.RegularExpressions;

namespace Statevia.Infrastructure.Modules;

/// <summary>テナント別 modules ルートの検証とパス解決。</summary>
/// <remarks>
/// <para>レイアウト正本: <c>{modulesRoot}/{tenantKey}/</c>。</para>
/// <para>
/// <c>tenant_key</c> 形式は Core-API の <c>TenantKeyValidator</c> と同規約
///（小文字・数字・ハイフン・ドット、最大 64 文字）。
/// </para>
/// </remarks>
public static partial class TenantModulePath
{
    /// <summary>tenant_key の最大長（DB / API と揃える）。</summary>
    public const int MaxTenantKeyLength = 64;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex TenantKeyPattern();

    /// <summary>tenant_key が形式として妥当か判定する。</summary>
    /// <param name="tenantKey">検証対象。</param>
    /// <returns>妥当なら <see langword="true"/>。</returns>
    public static bool IsValidTenantKey(string? tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return false;
        }

        var normalized = tenantKey.Trim();
        return normalized.Length is > 0 and <= MaxTenantKeyLength
            && TenantKeyPattern().IsMatch(normalized);
    }

    /// <summary>tenant_key を正規化し、不正なら例外を投げる。</summary>
    /// <param name="tenantKey">入力キー。</param>
    /// <returns>トリム済みキー。</returns>
    /// <exception cref="ArgumentException">形式不正のとき。</exception>
    public static string NormalizeTenantKey(string tenantKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        var normalized = tenantKey.Trim();
        if (normalized.Length > MaxTenantKeyLength)
        {
            throw new ArgumentException(
                $"tenantKey must be 1–{MaxTenantKeyLength} characters.",
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

    /// <summary>
    /// <paramref name="modulesRoot"/> 配下のテナント modules ルート絶対パスを返す。
    /// </summary>
    /// <param name="modulesRoot">共有 modules ルート。</param>
    /// <param name="tenantKey">テナントキー。</param>
    /// <returns><c>{modulesRoot}/{tenantKey}</c> の絶対パス。</returns>
    /// <exception cref="ArgumentException">
    /// tenant_key 不正、または解決結果が modules ルート外に出るとき。
    /// </exception>
    public static string ResolveTenantModulesRoot(string modulesRoot, string tenantKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulesRoot);

        var normalizedKey = NormalizeTenantKey(tenantKey);
        var rootFull = Path.GetFullPath(modulesRoot);
        var tenantRoot = Path.GetFullPath(Path.Combine(rootFull, normalizedKey));

        if (!IsStrictSubPathOf(tenantRoot, rootFull))
        {
            throw new ArgumentException(
                "Resolved tenant modules path escapes the modules root.",
                nameof(tenantKey));
        }

        return tenantRoot;
    }

    /// <summary><paramref name="candidate"/> が <paramref name="root"/> の真の子孫か。</summary>
    private static bool IsStrictSubPathOf(string candidate, string root)
    {
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
