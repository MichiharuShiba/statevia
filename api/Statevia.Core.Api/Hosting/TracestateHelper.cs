namespace Statevia.Core.Api.Hosting;

/// <summary>
/// W3C <c>tracestate</c> の既存値とベンダーメンバーをマージする（長さを抑える）。
/// </summary>
public static class TracestateHelper
{
    public const string StateviaVendorKey = "st@statevia";

    /// <summary>不透明値の上限（W3C の list-member 制限に合わせた実用的な上限）。</summary>
    public const int MaxOpaqueValueChars = 200;

    /// <summary>ヘッダ全体の実用的な上限。</summary>
    public const int MaxHeaderChars = 512;

    /// <summary>
    /// <paramref name="existing"/> から同一ベンダーキーのメンバーを除き、末尾に <paramref name="vendorKey"/>=<paramref name="opaqueValue"/> を追加する。
    /// </summary>
    public static string Merge(string? existing, string vendorKey, string opaqueValue)
    {
        if (string.IsNullOrEmpty(opaqueValue))
            return existing ?? "";

        var members = new List<string>();
        if (!string.IsNullOrEmpty(existing))
        {
            // 既存 list-member を分解し、同一ベンダーキーは上書き用に落とす
            foreach (var part in existing.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0 || eq >= part.Length - 1)
                    continue;
                var key = part[..eq].Trim();
                var val = part[(eq + 1)..].Trim();
                if (string.Equals(key, vendorKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                members.Add($"{key}={val}");
            }
        }

        members.Add($"{vendorKey}={opaqueValue}");
        var merged = string.Join(",", members);
        // W3C 推奨に近い実用的上限で切り詰め
        return merged.Length <= MaxHeaderChars ? merged : merged[..MaxHeaderChars];
    }
}
