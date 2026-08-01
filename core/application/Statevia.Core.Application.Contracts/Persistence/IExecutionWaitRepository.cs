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

    /// <summary>期限切れの DelayWait を取得する。</summary>
    /// <param name="utcNow">現在 UTC 時刻。</param>
    /// <param name="limit">最大件数。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>期限切れの wait 行一覧。</returns>
    Task<IReadOnlyList<ExecutionWaitRow>> ListExpiredDelayWaitsAsync(
        DateTime utcNow,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// 現在テナント内で topic / key に厳密一致する購読を取得する。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="topic">正規化済みトピック。</param>
    /// <param name="correlationKey">正規化済み key（未指定は空文字）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>配送対象の購読一致結果。</returns>
    Task<IReadOnlyList<MatchingWaitSubscription>> ListMatchingSubscriptionsAsync(
        ICoreUnitOfWork uow,
        string topic,
        string correlationKey,
        CancellationToken ct);
}
