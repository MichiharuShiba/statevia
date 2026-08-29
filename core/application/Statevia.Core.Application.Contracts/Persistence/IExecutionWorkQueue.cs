namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 実行ライフサイクル操作を非同期に配送する永続ワークキュー。
/// </summary>
public interface IExecutionWorkQueue
{
    /// <summary>ワーク項目をキューへ追加する（専用 DbContext で即コミット）。</summary>
    /// <param name="item">追加する項目。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <remarks>呼び出し側 UoW と原子性を取る場合は <see cref="EnqueueAsync(ICoreUnitOfWork, ExecutionWorkItemRow, CancellationToken)"/> を使う。</remarks>
    Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct);

    /// <summary>呼び出し側 UoW にワーク項目を追加する（SaveChanges / コミットは呼び出し側）。</summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="item">追加する項目。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task EnqueueAsync(ICoreUnitOfWork uow, ExecutionWorkItemRow item, CancellationToken ct);

    /// <summary>複数のワーク項目を同一コミットでキューへ追加する（専用 DbContext）。</summary>
    /// <param name="items">追加する項目。空なら何もしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <remarks>チャンク分割は負荷計測後に検討する。現状は呼び出し側の全件を一括投入する。</remarks>
    Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct);

    /// <summary>呼び出し側 UoW に複数ワーク項目を追加する（SaveChanges / コミットは呼び出し側）。</summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="items">追加する項目。空なら何もしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task EnqueueManyAsync(ICoreUnitOfWork uow, IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct);

    /// <summary>
    /// 処理可能で未 lease または期限切れの項目を排他取得する。
    /// </summary>
    /// <param name="leaseOwner">取得ワーカー ID。</param>
    /// <param name="utcNow">現在 UTC 時刻。</param>
    /// <param name="leaseDuration">lease 有効期間。</param>
    /// <param name="limit">最大取得件数。</param>
    /// <param name="kinds">取得する kind の許可リスト。null または空なら種別を問わない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>取得して lease を設定した項目。</returns>
    Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
        string leaseOwner,
        DateTime utcNow,
        TimeSpan leaseDuration,
        int limit,
        IReadOnlyList<string>? kinds,
        CancellationToken ct);

    /// <summary>
    /// 所有中の項目の lease 期限を延長する（処理中 heartbeat）。
    /// </summary>
    /// <param name="workItemId">ワーク項目 ID。</param>
    /// <param name="leaseOwner">lease 所有者（不一致なら更新しない）。</param>
    /// <param name="utcNow">現在 UTC 時刻。</param>
    /// <param name="leaseDuration">延長後の有効期間。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>延長できたとき <see langword="true"/>。所有者不一致・行不在なら <see langword="false"/>。</returns>
    Task<bool> RenewLeaseAsync(
        Guid workItemId,
        string leaseOwner,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken ct);

    /// <summary>正常処理済みの項目を削除する。</summary>
    /// <param name="workItemId">ワーク項目 ID。</param>
    /// <param name="leaseOwner">lease 所有者。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task CompleteAsync(Guid workItemId, string leaseOwner, CancellationToken ct);

    /// <summary>
    /// 指定実行の未完了 Start work item を削除する（lease の有無を問わない）。
    /// </summary>
    /// <param name="uow">同一トランザクションの Unit of Work。</param>
    /// <param name="executionId">対象実行 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <remarks>
    /// 未 Start Cancel 終端と同一 UoW で呼び、残 Start の再 claim による <c>engine.Start</c> を防ぐ。
    /// kind 語彙は変えない。
    /// </remarks>
    Task CompleteIncompleteStartItemsAsync(ICoreUnitOfWork uow, Guid executionId, CancellationToken ct);

    /// <summary>失敗した項目の lease を解放し、指定時刻以降へ再投入する。</summary>
    /// <param name="workItemId">ワーク項目 ID。</param>
    /// <param name="leaseOwner">lease 所有者。</param>
    /// <param name="availableAt">再投入可能となる UTC 時刻。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task ReleaseAsync(Guid workItemId, string leaseOwner, DateTime availableAt, CancellationToken ct);

    /// <summary>
    /// 期限切れ所有を世代 +1・所有者なしにしたうえで、同一トランザクションで
    /// <c>Resume mode=recovery</c> を enqueue する。
    /// </summary>
    /// <returns>投入した recovery 件数。</returns>
    Task<int> EnqueueExpiredOwnershipRecoveriesAsync(
        DateTime nowUtc,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// 期限切れ DelayWait を <c>FOR UPDATE SKIP LOCKED</c> で排他取得し、同一トランザクションで
    /// wait 行を削除したうえで <c>Resume mode=event</c>（delay.completed）を enqueue する。
    /// </summary>
    /// <remarks>
    /// 複数 Scheduler が並行スキャンしても同一 wait は一度だけ Resume される。
    /// 子購読行は FK CASCADE で削除される。
    /// </remarks>
    /// <returns>投入した Resume 件数。</returns>
    Task<int> EnqueueExpiredDelayWaitResumesAsync(
        DateTime nowUtc,
        int limit,
        CancellationToken ct);
}
