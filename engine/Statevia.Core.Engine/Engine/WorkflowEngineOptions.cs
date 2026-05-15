namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="WorkflowEngine"/> のランタイム調整オプション（チューニング値のみ）。
/// ロガーやファクトリ等の依存はコンストラクタ注入に固定する。
/// </summary>
public sealed class WorkflowEngineOptions
{
    /// <summary>
    /// 最大並列実行数。
    /// </summary>
    /// <remarks>
    /// ホストが <c>AddStateviaWorkflowEngine</c> 拡張で既定の <see cref="Scheduler.DefaultScheduler"/> を組み立てるときに参照する。既定値は 4。
    /// </remarks>
    public int MaxParallelism { get; set; } = 4;
}
