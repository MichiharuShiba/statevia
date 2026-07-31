namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 実行ライフサイクル操作を非同期に配送する永続ワークキュー。
/// </summary>
public interface IExecutionWorkQueue
{
    /// <summary>ワーク項目をキューへ追加する。</summary>
    /// <param name="item">追加する項目。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task EnqueueAsync(ExecutionWorkItemRow item, CancellationToken ct);

    /// <summary>複数のワーク項目を同一コミットでキューへ追加する。</summary>
    /// <param name="items">追加する項目。空なら何もしない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <remarks>チャンク分割は負荷計測後に検討する。現状は呼び出し側の全件を一括投入する。</remarks>
    Task EnqueueManyAsync(IReadOnlyList<ExecutionWorkItemRow> items, CancellationToken ct);

    /// <summary>
    /// 処理可能で未 lease または期限切れの項目を排他取得する。
    /// </summary>
    /// <param name="leaseOwner">取得ワーカー ID。</param>
    /// <param name="utcNow">現在 UTC 時刻。</param>
    /// <param name="leaseDuration">lease 有効期間。</param>
    /// <param name="limit">最大取得件数。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>取得して lease を設定した項目。</returns>
    Task<IReadOnlyList<ExecutionWorkItemRow>> ClaimAsync(
        string leaseOwner,
        DateTime utcNow,
        TimeSpan leaseDuration,
        int limit,
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

    /// <summary>失敗した項目の lease を解放し、指定時刻以降へ再投入する。</summary>
    /// <param name="workItemId">ワーク項目 ID。</param>
    /// <param name="leaseOwner">lease 所有者。</param>
    /// <param name="availableAt">再投入可能となる UTC 時刻。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task ReleaseAsync(Guid workItemId, string leaseOwner, DateTime availableAt, CancellationToken ct);
}
