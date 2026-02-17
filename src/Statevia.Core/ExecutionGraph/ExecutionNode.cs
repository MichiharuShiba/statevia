namespace Statevia.Core.ExecutionGraphs;

/// <summary>
/// 実行グラフのノード。1 回の状態実行を表します。
/// デバッグ・可視化用であり、実行には影響しません。
/// </summary>
public sealed class ExecutionNode
{
    /// <summary>ノード一意識別子。</summary>
    public required string NodeId { get; init; }
    /// <summary>状態名。</summary>
    public required string StateName { get; init; }
    /// <summary>開始日時。</summary>
    public required DateTime StartedAt { get; init; }
    /// <summary>完了日時。未完了なら null。</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>完了時の事実（Completed / Failed / Cancelled / Joined など）。</summary>
    public string? Fact { get; set; }
    /// <summary>状態の出力。Join で参照可能。</summary>
    public object? Output { get; set; }
}
