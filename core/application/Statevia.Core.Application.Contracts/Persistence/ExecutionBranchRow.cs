namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <c>execution_branches.status</c> の許容値（Join 集約用の投影）。
/// </summary>
/// <remarks>
/// <para><c>executions.status</c> の終端語彙（Running / Completed / Failed / Cancelled）に揃える。</para>
/// </remarks>
public static class ExecutionBranchStatuses
{
    /// <summary>子が未終端。</summary>
    public const string Running = "Running";

    /// <summary>子が正常終端。</summary>
    public const string Completed = "Completed";

    /// <summary>子が失敗終端。</summary>
    public const string Failed = "Failed";

    /// <summary>子がキャンセル終端。</summary>
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// <c>execution_branches</c> 行。親 execution と Fork 分岐子 execution の関係正本。
/// </summary>
/// <remarks>
/// <para>
/// 一意: <c>(parent_execution_id, fork_node_id, branch_state)</c> および <c>execution_id</c>。
/// <c>fork_node_id</c> は親実行グラフ上の Fork 到達インスタンス（実行ノード ID）。
/// <c>join_state</c> / <c>branch_state</c> は定義の状態名空間。
/// </para>
/// </remarks>
public sealed class ExecutionBranchRow
{
    /// <summary>Fork に到達した親（ネスト時は親役）execution。</summary>
    public Guid ParentExecutionId { get; set; }

    /// <summary>分岐を走らせる子 execution。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>親実行グラフ上の Fork ノード ID（到達インスタンス）。</summary>
    public required string ForkNodeId { get; set; }

    /// <summary>Join 状態名。</summary>
    public required string JoinState { get; set; }

    /// <summary>分岐先頭状態名。</summary>
    public required string BranchState { get; set; }

    /// <summary>Join 集約用の完了事実要約（<see cref="ExecutionBranchStatuses"/>）。</summary>
    public required string Status { get; set; }

    /// <summary>終端時に親へ渡す output の JSON（任意）。</summary>
    public string? OutputJson { get; set; }

    /// <summary>行作成時刻（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>行更新時刻（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary><c>execution_branches</c> の status / output 更新用 DTO。</summary>
/// <param name="Status">新しい status（<see cref="ExecutionBranchStatuses"/>）。</param>
/// <param name="UpdatedAt">更新時刻（UTC）。</param>
/// <param name="OutputJson">終端 output JSON。null のとき列は変更しない。</param>
public sealed record ExecutionBranchStatusUpdate(
    string Status,
    DateTime UpdatedAt,
    string? OutputJson);
