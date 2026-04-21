namespace Statevia.Core.Engine.Definition;

/// <summary>
/// 条件式 <c>when.op</c> を評価器・検証で共通利用する正規形（大文字の英語略号）へ揃える。
/// </summary>
internal static class ConditionExpressionOperatorNormalizer
{
    private static readonly HashSet<string> CanonicalOperators =
    [
        "EQ",
        "NE",
        "GT",
        "GTE",
        "LT",
        "LTE",
        "EXISTS",
        "IN",
        "BETWEEN"
    ];

    /// <summary>
    /// トリムした <paramref name="rawOp"/> を正規形へ変換する。
    /// 記号（<c>=</c>、<c>!=</c> / <c>&lt;&gt;</c> など）と既存の英語名（<c>eq</c> など）の両方を受け付ける。
    /// </summary>
    /// <param name="rawOp">YAML / JSON から読み取った演算子文字列。</param>
    /// <param name="canonicalOp">正規形（例: <c>EQ</c>）。失敗時は未定義。</param>
    /// <returns>正規形へマップできたとき true。</returns>
    public static bool TryNormalize(string? rawOp, out string canonicalOp)
    {
        canonicalOp = string.Empty;
        if (string.IsNullOrWhiteSpace(rawOp))
        {
            return false;
        }

        var trimmed = rawOp.Trim();
        switch (trimmed)
        {
            case "=":
            case "==":
                canonicalOp = "EQ";
                return true;
            case "!=":
            case "<>":
                canonicalOp = "NE";
                return true;
            case ">":
                canonicalOp = "GT";
                return true;
            case ">=":
                canonicalOp = "GTE";
                return true;
            case "<":
                canonicalOp = "LT";
                return true;
            case "<=":
                canonicalOp = "LTE";
                return true;
        }

        var upper = trimmed.ToUpperInvariant();
        if (CanonicalOperators.Contains(upper))
        {
            canonicalOp = upper;
            return true;
        }

        return false;
    }
}
