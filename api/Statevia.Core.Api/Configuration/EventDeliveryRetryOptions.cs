namespace Statevia.Core.Api.Configuration;

/// <summary>
/// <c>event_delivery_dedup</c> 先行 INSERT など、DB 一時障害に対する再試行の設定。
/// </summary>
internal sealed class EventDeliveryRetryOptions
{
    /// <summary>同一操作あたりの最大試行回数（初回含む）。既定は 3。</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>初回失敗後の待機の基準となる遅延（ミリ秒）。指数バックオフの起点。</summary>
    public int BaseDelayMs { get; set; } = 50;

    /// <summary>待機時間の上限（ミリ秒）。</summary>
    public int MaxDelayMs { get; set; } = 2000;

    /// <summary>true のとき指数バックオフにランダム成分を乗せる。</summary>
    public bool Jitter { get; set; } = true;

    /// <summary>
    /// 再試行で <see cref="Task.Delay(int)"/> する合計時間の上限（ミリ秒）。
    /// API リクエストのタイムアウト予算を超えないようクリップするために用いる。0 は上限なし。
    /// </summary>
    public int MaxTotalBackoffMs { get; set; } = 8000;

    /// <summary>
    /// イベント配送・キャンセル等の Serializable 永続化ブロックの最大試行回数（初回含む）。
    /// PostgreSQL の直列化失敗（40001）やデッドロック（40P01）の再試行上限。既定は 8。
    /// </summary>
    public int SerializablePersistenceMaxAttempts { get; set; } = 8;
}
