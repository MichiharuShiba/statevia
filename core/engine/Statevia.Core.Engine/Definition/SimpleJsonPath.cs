namespace Statevia.Core.Engine.Definition;

/// <summary>
/// 定義・実行で共有する簡易 JSONPath の構文検証とセグメント分解。
/// </summary>
/// <remarks>
/// <para>サポート: ドット識別子、<c>['key']</c>/<c>["key"]</c>、非負整数インデックス <c>[0]</c>。</para>
/// <para>未サポート（IsValid=false）: ワイルドカード・負数・スライス・フィルタ・空白付きブラケット等。</para>
/// </remarks>
public static class SimpleJsonPath
{
    /// <summary>
    /// 配列インデックスの最大桁数。
    /// </summary>
    /// <remarks>
    /// <c>int</c> 範囲内に収まる桁と、異常に長い数字リテラルによるパース負荷を抑えるため 10 桁とする。
    /// </remarks>
    internal const int MaxIndexDigits = 10;

    /// <summary>
    /// 文字列がパス式として解釈候補か（<c>$</c> / <c>$.…</c> / <c>$[…]</c>）を返す。
    /// 妥当性までは見ない（リテラルとの区別用）。
    /// </summary>
    /// <param name="path">検査する文字列。</param>
    /// <returns>パス式候補なら true。</returns>
    public static bool IsPathExpression(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path == "$"
            || path.StartsWith("$.", StringComparison.Ordinal)
            || path.StartsWith("$[", StringComparison.Ordinal);
    }

    /// <summary>
    /// <paramref name="path"/> が受理可能な簡易 JSONPath かを返す。
    /// </summary>
    /// <param name="path">検査するパス文字列。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsValid(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return TryGetSegments(path, out _);
    }

    /// <summary>
    /// パスをセグメント列へ分解する。<c>$</c> のみのときは空リスト。
    /// </summary>
    /// <param name="path">パス文字列。</param>
    /// <param name="segments">分解結果。失敗時は空。</param>
    /// <returns>分解に成功したとき true。</returns>
    internal static bool TryGetSegments(string path, out IReadOnlyList<PathSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(path);
        segments = Array.Empty<PathSegment>();

        if (path == "$")
        {
            return true;
        }

        if (!IsPathExpression(path) || path.EndsWith('.'))
        {
            return false;
        }

        var list = new List<PathSegment>();
        var pos = 1; // after '$'
        while (pos < path.Length)
        {
            if (!TryConsumeNextSegment(path, ref pos, list))
            {
                return false;
            }
        }

        if (list.Count == 0)
        {
            return false;
        }

        segments = list;
        return true;
    }

    /// <summary>
    /// <c>.</c> 区切りまたはブラケット区切りの次セグメントを読み進める。
    /// </summary>
    private static bool TryConsumeNextSegment(string path, ref int pos, List<PathSegment> list)
    {
        var ch = path[pos];
        if (ch == '.')
        {
            pos++;
            return pos < path.Length && TryReadSegment(path, ref pos, list);
        }

        if (ch == '[')
        {
            return TryReadBracketSegment(path, ref pos, list);
        }

        return false;
    }

    private static bool TryReadSegment(string path, ref int pos, List<PathSegment> list)
    {
        if (pos >= path.Length)
        {
            return false;
        }

        if (path[pos] == '[')
        {
            return TryReadBracketSegment(path, ref pos, list);
        }

        return TryReadIdentifierSegment(path, ref pos, list);
    }

    private static bool TryReadIdentifierSegment(string path, ref int pos, List<PathSegment> list)
    {
        var start = pos;
        while (pos < path.Length)
        {
            var ch = path[pos];
            if (!(char.IsLetterOrDigit(ch) || ch == '_'))
            {
                break;
            }

            pos++;
        }

        if (pos == start)
        {
            return false;
        }

        list.Add(PathSegment.ForIdentifier(path[start..pos]));
        return true;
    }

    private static bool TryReadBracketSegment(string path, ref int pos, List<PathSegment> list)
    {
        if (pos >= path.Length || path[pos] != '[')
        {
            return false;
        }

        pos++; // skip [
        if (pos >= path.Length)
        {
            return false;
        }

        var quote = path[pos];
        if (quote is '\'' or '"')
        {
            return TryReadQuotedKeyBracket(path, ref pos, list, quote);
        }

        return TryReadArrayIndexBracket(path, ref pos, list);
    }

    private static bool TryReadQuotedKeyBracket(string path, ref int pos, List<PathSegment> list, char quote)
    {
        pos++; // skip opening quote
        var start = pos;
        while (pos < path.Length && path[pos] != quote)
        {
            pos++;
        }

        if (pos >= path.Length)
        {
            return false;
        }

        var key = path[start..pos];
        if (key.Length == 0)
        {
            return false;
        }

        pos++; // skip closing quote
        if (pos >= path.Length || path[pos] != ']')
        {
            return false;
        }

        pos++; // skip ]
        list.Add(PathSegment.ForQuotedKey(key));
        return true;
    }

    /// <summary>
    /// 引用なしブラケットを配列インデックスとして読む。
    /// </summary>
    /// <remarks>
    /// 先頭ゼロは <c>0</c> のみ許可（<c>01</c> は不正）。桁数は <see cref="MaxIndexDigits"/> 以下で <c>int</c> に収まること。
    /// </remarks>
    private static bool TryReadArrayIndexBracket(string path, ref int pos, List<PathSegment> list)
    {
        var start = pos;
        while (pos < path.Length && char.IsAsciiDigit(path[pos]))
        {
            pos++;
        }

        var digitCount = pos - start;
        if (digitCount is 0 or > MaxIndexDigits)
        {
            return false;
        }

        if (pos >= path.Length || path[pos] != ']')
        {
            return false;
        }

        var digits = path[start..pos];
        if (digits.Length > 1 && digits[0] == '0')
        {
            return false;
        }

        if (!int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
            || index < 0)
        {
            return false;
        }

        pos++; // skip ]
        list.Add(PathSegment.ForArrayIndex(index));
        return true;
    }
}
