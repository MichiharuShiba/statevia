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
}
