namespace Statevia.Core.Engine.Definition;

/// <summary>
/// Engine が受理する簡易 JSONPath（<c>$</c> または <c>$.seg1.seg2</c>）の妥当性判定。
/// </summary>
public static class SimpleJsonPath
{
    /// <summary>
    /// <paramref name="path"/> が <c>$</c> または <c>$.</c> で始まる英数字・アンダースコア区切りのパスとして有効かを返す。
    /// </summary>
    /// <param name="path">検査するパス文字列。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsValid(string path)
    {
        if (path == "$")
        {
            return true;
        }

        if (!path.StartsWith("$.", StringComparison.Ordinal) || path.EndsWith('.'))
        {
            return false;
        }

        var segments = path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        foreach (var seg in segments)
        {
            if (seg.Length == 0 || seg.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            {
                return false;
            }
        }

        return true;
    }
}
