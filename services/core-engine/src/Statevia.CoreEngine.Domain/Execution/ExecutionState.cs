using Statevia.CoreEngine.Domain.Node;

namespace Statevia.CoreEngine.Domain.Execution;

/// <summary>
/// 実行の集約状態。Read Model と整合するフィールド（status, cancelRequestedAt, nodes 等）。
/// data-integration-contract §2.1 / core-reducer-spec §2。
/// </summary>
/// <param name="ExecutionId">実行識別子。</param>
/// <param name="GraphId">グラフ識別子。</param>
/// <param name="Status">実行状態。</param>
/// <param name="Nodes">nodeId → NodeState の辞書。</param>
/// <param name="Version">楽観ロック用バージョン。</param>
/// <param name="CancelRequestedAt">Cancel 要求日時（ISO8601 文字列）。</param>
/// <param name="CanceledAt">Cancel 完了日時。</param>
/// <param name="FailedAt">失敗日時。</param>
/// <param name="CompletedAt">完了日時。</param>
public sealed record ExecutionState(
    string ExecutionId,
    string GraphId,
    ExecutionStatus Status,
    IReadOnlyDictionary<string, NodeState> Nodes,
    int Version,
    string? CancelRequestedAt = null,
    string? CanceledAt = null,
    string? FailedAt = null,
    string? CompletedAt = null);
