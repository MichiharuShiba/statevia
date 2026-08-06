namespace Statevia.Core.Engine.Abstractions;

/// <summary>Fork 物理展開ハンドラへ渡す 1 分岐分の計画。</summary>
/// <param name="BranchState">分岐先頭状態名。</param>
/// <param name="MappedInput">親 Context で評価済みの入力。</param>
public sealed record ForkBranchExpansionPlan(string BranchState, object? MappedInput);

/// <summary>
/// Hosted Runtime が論理 Fork の代わりに子 execution を起動するときの通知。
/// </summary>
/// <remarks>
/// <para>ハンドラ未登録時は Engine が従来どおり同一インスタンス上で分岐を Schedule する。</para>
/// <para>ハンドラ登録時は Engine は分岐を Schedule せず、本イベントのみ発火する。</para>
/// </remarks>
/// <param name="ExecutionId">親 execution ID（Engine 上の文字列 ID）。</param>
/// <param name="ForkState">Fork 遷移元の状態名（ForkTable キー）。</param>
/// <param name="SourceNodeId">Fork 遷移を起こしたグラフノード ID。</param>
/// <param name="Branches">分岐先頭と写像済み input。</param>
public sealed record ForkExpansionEvent(
    string ExecutionId,
    string ForkState,
    string SourceNodeId,
    IReadOnlyList<ForkBranchExpansionPlan> Branches);
