namespace Statevia.Core.Api.Configuration;

/// <summary>
/// ワークフロー投影更新キューの設定。
/// </summary>
public sealed class WorkflowProjectionQueueOptions
{
    /// <summary>
    /// グローバル待ち行列の最大スロット数。
    /// </summary>
    public int MaxGlobalQueueSize { get; set; } = 16_384;

    /// <summary>
    /// 同一ワークフロー要求をまとめるデバウンス時間（ms）。
    /// </summary>
    public int ProjectionFlushDebounceMs { get; set; } = 50;

    /// <summary>
    /// 同一ワークフロー投影更新の連続失敗に対する最大再試行回数。
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// 再試行バックオフの最小待機時間（ms）。
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>
    /// 再試行バックオフの最大待機時間（ms）。
    /// </summary>
    public int RetryMaxDelayMs { get; set; } = 5000;
}
