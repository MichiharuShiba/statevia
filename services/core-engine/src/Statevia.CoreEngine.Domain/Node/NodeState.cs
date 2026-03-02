namespace Statevia.CoreEngine.Domain.Node;

/// <summary>
/// ノードの状態。Read Model と整合するフィールド（status, attempt, workerId, waitKey, canceledByExecution 等）。
/// data-integration-contract §2.1 / core-reducer-spec §2。
/// </summary>
/// <param name="NodeId">ノード識別子。</param>
/// <param name="NodeType">ノード種別（Task, Wait, Fork, Join 等）。</param>
/// <param name="Status">ノード状態。</param>
/// <param name="Attempt">試行回数。</param>
/// <param name="WorkerId">ワーカー識別子（任意）。</param>
/// <param name="WaitKey">待機キー（任意）。</param>
/// <param name="Output">出力（任意）。</param>
/// <param name="Error">エラー情報（任意）。</param>
/// <param name="CanceledByExecution">Execution の Cancel により実質キャンセル扱いになったか。</param>
public sealed record NodeState(
    string NodeId,
    string NodeType,
    NodeStatus Status,
    int Attempt,
    string? WorkerId = null,
    string? WaitKey = null,
    object? Output = null,
    object? Error = null,
    bool CanceledByExecution = false);
