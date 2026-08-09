namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary><c>execution_branches</c> 永続化。</summary>
public interface IExecutionBranchRepository
{
    /// <summary>
    /// 分岐行を冪等挿入する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// キー <c>(parent_execution_id, fork_node_id, branch_state)</c> が未存在なら挿入する。
    /// 既存かつ <see cref="ExecutionBranchRow.ExecutionId"/> が一致すれば何もしない。
    /// 既存かつ <c>execution_id</c> が異なれば <see cref="InvalidOperationException"/>。
    /// </para>
    /// </remarks>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="branches">挿入する分岐行。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task InsertBranchesIdempotentAsync(
        ICoreUnitOfWork uow,
        IReadOnlyList<ExecutionBranchRow> branches,
        CancellationToken ct);

    /// <summary>
    /// 親 execution 配下の分岐行を作成時刻順で返す。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>分岐行一覧。</returns>
    Task<IReadOnlyList<ExecutionBranchRow>> ListByParentExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid parentExecutionId,
        CancellationToken ct);

    /// <summary>
    /// 親・Fork 到達インスタンス単位の分岐行を返す。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="forkNodeId">親実行グラフ上の Fork ノード ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>当該 Fork 到達の分岐行一覧。</returns>
    Task<IReadOnlyList<ExecutionBranchRow>> ListByParentAndForkNodeAsync(
        ICoreUnitOfWork uow,
        Guid parentExecutionId,
        string forkNodeId,
        CancellationToken ct);

    /// <summary>
    /// 子 execution_id で分岐行を取得する。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="childExecutionId">子 execution。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>分岐行。無ければ null。</returns>
    Task<ExecutionBranchRow?> GetByChildExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid childExecutionId,
        CancellationToken ct);

    /// <summary>
    /// 分岐の status / output を更新する。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="forkNodeId">親実行グラフ上の Fork ノード ID。</param>
    /// <param name="branchState">分岐先頭状態名。</param>
    /// <param name="update">status / updatedAt / output の更新内容。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>更新できたとき true。行が無いとき false。</returns>
    Task<bool> TryUpdateStatusAsync(
        ICoreUnitOfWork uow,
        Guid parentExecutionId,
        string forkNodeId,
        string branchState,
        ExecutionBranchStatusUpdate update,
        CancellationToken ct);
}
