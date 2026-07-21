namespace Statevia.Core.Engine.Definition;

/// <summary>
/// Engine が受理する簡易 JSONPath の妥当性判定とセグメント分解。
/// </summary>
/// <remarks>
/// <para>受理形:</para>
/// <list type="bullet">
/// <item><c>$</c></item>
/// <item><c>$.seg1.seg2</c>（セグメントは英数字と <c>_</c>）</item>
/// <item><c>$.states['order.notify.customer'].output</c>（ブラケット＋単一/二重引用。キーにドット等を含められる）</item>
/// <item><c>$['input'].x</c>（ルート直後のブラケット）</item>
/// </list>
/// </remarks>
public static class SimpleJsonPath
{
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
    internal static bool TryGetSegments(string path, out IReadOnlyList<string> segments)
    {
        ArgumentNullException.ThrowIfNull(path);
        segments = Array.Empty<string>();

        if (path == "$")
        {
            return true;
        }

        if (!IsPathExpression(path) || path.EndsWith('.'))
        {
            return false;
        }

        var list = new List<string>();
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
    private static bool TryConsumeNextSegment(string path, ref int pos, List<string> list)
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

    private static bool TryReadSegment(string path, ref int pos, List<string> list)
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

    private static bool TryReadIdentifierSegment(string path, ref int pos, List<string> list)
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

        list.Add(path[start..pos]);
        return true;
    }

    private static bool TryReadBracketSegment(string path, ref int pos, List<string> list)
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
        if (quote is not ('\'' or '"'))
        {
            return false;
        }

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
        list.Add(key);
        return true;
    }
}
