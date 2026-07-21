namespace Statevia.Core.Engine.Engine;

/// <summary>
/// Execution Context（パス評価根 <c>$</c>）の固定キー名。
/// </summary>
/// <remarks>
/// <para>
/// 仕様上のトップレベルは <see cref="Input"/> / <see cref="Output"/> /
/// <see cref="States"/> / <see cref="Vars"/> / <see cref="Sys"/> のみ。
/// 定義 YAML の <c>states:</c> キー等とは別レイヤーである。
/// </para>
/// <para>
/// Phase 1 では <see cref="Vars"/> / <see cref="Sys"/> への input.path 参照は
/// Compiler（Level1）で拒否する。
/// </para>
/// </remarks>
public static class ExecutionContextKeys
{
    /// <summary>ワークフロー開始 input（<c>$.input</c>）。</summary>
    public const string Input = "input";

    /// <summary>ワークフロー終端 output（<c>$.output</c>）。</summary>
    public const string Output = "output";

    /// <summary>完了済み State のマップ（<c>$.states</c>）。</summary>
    public const string States = "states";

    /// <summary>実行変数（Phase 1 は空オブジェクト・input.path 参照不可）。</summary>
    public const string Vars = "vars";

    /// <summary>システム情報（Phase 1 は空オブジェクト・input.path 参照不可）。</summary>
    public const string Sys = "sys";
}
