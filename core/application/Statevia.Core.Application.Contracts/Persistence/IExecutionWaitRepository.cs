namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>execution_waits 永続化。</summary>
public interface IExecutionWaitRepository
{
    /// <summary>
    /// 指定 execution の wait 行を置換する（不在 node_id を削除し、指定行を upsert）。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象 execution。</param>
    /// <param name="waits">残したい wait 行（1 Wait = 1 行）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct);

    /// <summary>
    /// 指定 node_id の wait 行を削除する（Resume 成功時）。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象 execution。</param>
    /// <param name="nodeId">削除する Wait ノード ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task DeleteByNodeIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string nodeId,
        CancellationToken ct);

    /// <summary>
    /// 指定 execution の wait 行を created_at / node_id 順で返す。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象 execution。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>wait 行一覧。</returns>
    Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);
}
