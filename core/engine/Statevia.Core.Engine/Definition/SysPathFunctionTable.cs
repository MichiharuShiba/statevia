using System.Globalization;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// Statevia 公式の <c>$.sys</c> CallPath 許可リスト。
/// </summary>
/// <remarks>
/// <para>関数追加はこのテーブルへ 1 エントリを足す。パーサ／Level1／Resolver は変更不要。</para>
/// <para>ユーザーや Module からの動的登録は行わない（Engine 内の静的公式リストのみ）。</para>
/// </remarks>
internal static class SysPathFunctionTable
{
    /// <summary>公式 CallPath 関数の 1 エントリ。</summary>
    /// <param name="Name">path 上の関数名（例: <c>now</c>）。</param>
    /// <param name="ArgumentPlaceholder">
    /// Level1 ヒント用の引数プレースホルダ（例: 日時書式なら <c>pattern</c>）。構文上は二重引用符文字列 1 個。
    /// </param>
    /// <param name="Evaluate">文字列引数を受け取り結果を返す。失敗時は例外を投げてよい。</param>
    internal sealed record Entry(
        string Name,
        string ArgumentPlaceholder,
        Func<string, string> Evaluate);

    /// <summary>
    /// 許可された関数名 → 評価器。
    /// </summary>
    /// <remarks>キー比較は序数（大文字小文字を区別）。</remarks>
    public static IReadOnlyDictionary<string, Entry> ByName { get; } =
        new Dictionary<string, Entry>(StringComparer.Ordinal)
        {
            ["now"] = new(
                Name: "now",
                ArgumentPlaceholder: "pattern",
                Evaluate: static argument => DateTimeOffset.Now.ToString(argument, CultureInfo.InvariantCulture)),
            ["utcNow"] = new(
                Name: "utcNow",
                ArgumentPlaceholder: "pattern",
                Evaluate: static argument => DateTimeOffset.UtcNow.ToString(argument, CultureInfo.InvariantCulture)),
        };

    /// <summary>許可されている関数エントリ（名前順の列挙用）。</summary>
    public static IEnumerable<Entry> Entries => ByName.Values;
}
