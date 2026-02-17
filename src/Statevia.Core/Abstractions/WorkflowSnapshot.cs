namespace Statevia.Core.Abstractions;

/// <summary>
/// ワークフローインスタンスの現在のスナップショット。
/// 観測用であり、実行には影響しません。
/// </summary>
public sealed class WorkflowSnapshot
{
    /// <summary>ワークフローインスタンス ID。</summary>
    public required string WorkflowId { get; init; }

    /// <summary>ワークフロー定義名。</summary>
    public required string WorkflowName { get; init; }

    /// <summary>現在アクティブな状態名の一覧。</summary>
    public required IReadOnlyList<string> ActiveStates { get; init; }

    /// <summary>end 状態に到達して完了したか。</summary>
    public required bool IsCompleted { get; init; }

    /// <summary>協調的キャンセルにより停止したか。</summary>
    public required bool IsCancelled { get; init; }

    /// <summary>失敗により停止したか。</summary>
    public required bool IsFailed { get; init; }
}
