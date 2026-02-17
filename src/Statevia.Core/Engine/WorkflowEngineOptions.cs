namespace Statevia.Core.Engine;

/// <summary>
/// WorkflowEngine のオプションです。
/// </summary>
public sealed class WorkflowEngineOptions
{
    /// <summary>
    /// 最大並列実行数。既定値は 4。
    /// </summary>
    public int MaxParallelism { get; set; } = 4;
}
