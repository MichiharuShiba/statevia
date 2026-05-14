namespace Statevia.Core.Engine.Engine;

/// <summary>
/// <see cref="WorkflowEngine"/> のランタイム調整オプション（チューニング値のみ）。
/// ロガーやファクトリ等の依存はコンストラクタ注入に固定する。
/// </summary>
public sealed class WorkflowEngineOptions
{
    /// <summary>
    /// 最大並列実行数（ホストが <c>AddStateviaWorkflowEngine</c> で既定スケジューラを構築するときに解釈）。既定値は 4。
    /// </summary>
    public int MaxParallelism { get; set; } = 4;
}
