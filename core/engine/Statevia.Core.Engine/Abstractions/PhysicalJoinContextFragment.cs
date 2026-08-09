namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// Hosted 物理 Join 充足時に親 Execution Context へ投影する子 1 件分の断片。
/// </summary>
/// <remarks>
/// <para>リスト順が適用順。同一 <c>states</c> 名・同一 <c>vars</c> キーは後勝ち（D5）。</para>
/// </remarks>
/// <param name="States">子の完了済み State エントリ（stateName → `{ output: … }`）。</param>
/// <param name="Vars">子終端時点の vars オブジェクト（辞書想定）。</param>
public sealed record PhysicalJoinContextFragment(
    IReadOnlyDictionary<string, object?>? States,
    object? Vars);
