using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Engine.ExecutionGraphs;

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
    /// <summary>ノード種別（Start/Task/Fork/Join/Wait/End）。</summary>
    public string NodeType { get; set; } = "Task";
    /// <summary>開始日時。</summary>
    public required DateTime StartedAt { get; init; }
    /// <summary>完了日時。未完了なら null。</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>完了時の事実（Completed / Failed / Cancelled / Joined など）。</summary>
    public string? Fact { get; set; }
    /// <summary>状態の出力。Join で参照可能。</summary>
    public object? Output { get; set; }
    /// <summary>状態の入力（状態実行時に渡された値）。</summary>
    public object? Input { get; set; }
    /// <summary>試行回数（現時点では 1 固定）。</summary>
    public int Attempt { get; set; } = 1;
    /// <summary>ワーカー識別子。現時点では実行ノード ID を格納する。</summary>
    public string? WorkerId { get; set; }
    /// <summary>待機キー（Wait 系状態で設定される想定。未設定時は null）。</summary>
    public string? WaitKey { get; set; }
    /// <summary>実行全体によりキャンセルされたかどうか。</summary>
    public bool CanceledByExecution { get; set; }

    /// <summary>
    /// 事実遷移が output 条件（<c>cases</c> / <c>default</c>）で解決されたときの診断。FSM のみのときは null。
    /// </summary>
    public ConditionRoutingDiagnostics? ConditionRouting { get; set; }
}
