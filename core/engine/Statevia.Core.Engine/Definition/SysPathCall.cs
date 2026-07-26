using System.Text.RegularExpressions;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// <c>$.sys.&lt;name&gt;("…")</c> の関数呼び出しパス構文。
/// </summary>
/// <remarks>
/// <para>Level1 検証と <see cref="ExecutionContextPathResolver"/> で共有する。</para>
/// <para>構文は汎用パース（二重引用符の文字列引数 1 個）し、許可名は <see cref="SysPathFunctionTable"/> で判定する。</para>
/// <para>完全一致のみ受理し、呼び出し後のドット連結・単引用符は拒否する。</para>
/// </remarks>
internal static partial class SysPathCall
{
    /// <summary>
    /// 文字列引数の最大長（二重引用符の内側）。
    /// </summary>
    /// <remarks>
    /// 一般的な引数（日時パターン等）の上限と、異常に長い引数による負荷回避のため 64 とする。
    /// </remarks>
    public const int MaxArgumentLength = 64;

    /// <summary>実行時に CallPath 評価が失敗したときの警告定数。</summary>
    public const string EvaluationFailedWarning = "SysPathCallEvaluationFailed";

    private const string SysPrefix = "$.sys.";

    /// <summary>分解済みの CallPath。</summary>
    /// <param name="FunctionName">許可リスト上の関数名。</param>
    /// <param name="Argument">二重引用符内側の文字列引数（1..<see cref="MaxArgumentLength"/>）。</param>
    internal readonly record struct Info(string FunctionName, string Argument);

    /// <summary>
    /// <paramref name="path"/> が正当な CallPath か判定する（Level1 用）。
    /// </summary>
    /// <param name="path">検査対象パス。</param>
    /// <returns>完全一致かつ許可テーブルに載るとき true。</returns>
    public static bool IsValidCallPath(string path) => TryParse(path, out _);

    /// <summary>
    /// <c>$.sys.</c> で始まり <c>(</c> を含む関数呼び出し候補か。
    /// </summary>
    /// <param name="path">検査対象パス。</param>
    /// <returns>候補のとき true（構文正当・許可済みとは限らない）。</returns>
    public static bool IsCallPathCandidate(string path)
        => !string.IsNullOrEmpty(path)
           && path.StartsWith(SysPrefix, StringComparison.Ordinal)
           && path.Contains('(', StringComparison.Ordinal);

    /// <summary>
    /// Level1 エラー用に、許可 CallPath の説明文を返す。
    /// </summary>
    /// <returns>許可例と文字列引数制約を含む英語メッセージ断片。</returns>
    public static string FormatAllowedCallPathsHint()
    {
        var examples = SysPathFunctionTable.Entries
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
            .Select(static entry => $"$.sys.{entry.Name}(\"{entry.ArgumentPlaceholder}\")");

        return "allowed call paths are " +
               string.Join(" and ", examples) +
               $" (string argument length 1..{MaxArgumentLength}, double quotes only)";
    }

    /// <summary>
    /// CallPath を完全一致で分解し、許可テーブルで解決する。
    /// </summary>
    /// <param name="path">検査対象パス。</param>
    /// <param name="info">成功時の分解結果。</param>
    /// <returns>構文正当かつ許可名のとき true。</returns>
    public static bool TryParse(string path, out Info info)
    {
        info = default;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var match = CallPathRegex().Match(path);
        if (!match.Success)
        {
            return false;
        }

        var functionName = match.Groups["fn"].Value;
        var argument = match.Groups["arg"].Value;
        if (argument.Length is < 1 or > MaxArgumentLength)
        {
            return false;
        }

        if (!SysPathFunctionTable.ByName.ContainsKey(functionName))
        {
            return false;
        }

        info = new Info(functionName, argument);
        return true;
    }

    /// <summary>
    /// CallPath を評価し、テーブル上の評価器で結果文字列を返す。
    /// </summary>
    /// <param name="info">分解済み CallPath。</param>
    /// <param name="value">成功時の結果。</param>
    /// <returns>成功時 true。評価器が失敗したとき false。</returns>
    public static bool TryEvaluate(Info info, out string? value)
    {
        value = null;
        if (!SysPathFunctionTable.ByName.TryGetValue(info.FunctionName, out var entry))
        {
            return false;
        }

        try
        {
            value = entry.Evaluate(info.Argument);
            return true;
        }
        catch (FormatException)
        {
            // 日時パターン等の .NET 書式評価失敗。関数固有の失敗は評価器側でこの例外に寄せる。
            return false;
        }
    }

    // 関数名は識別子形。許可可否は SysPathFunctionTable で判定する（regex に関数名を埋め込まない）。
    [GeneratedRegex(
        @"^\$\.sys\.(?<fn>[A-Za-z_][A-Za-z0-9_]*)\(""(?<arg>[^""]{1,64})""\)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex CallPathRegex();
}
