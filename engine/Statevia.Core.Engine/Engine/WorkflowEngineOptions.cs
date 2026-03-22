using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Infrastructure;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// WorkflowEngine のオプションです。
/// </summary>
public sealed class WorkflowEngineOptions
{
    /// <summary>
    /// 最大並列実行数。既定値は 4。
    /// </summary>
    public int MaxParallelism { get; set; } = 4;

    /// <summary>
    /// <see cref="IWorkflowEngine.Start"/> で <c>workflowId</c> が未指定のときの ID 生成。
    /// 未設定時は <see cref="UuidV7WorkflowInstanceIdGenerator"/>（Core-API の UUID v7 と同趣旨）。
    /// </summary>
    public IWorkflowInstanceIdGenerator? WorkflowInstanceIdGenerator { get; set; }
}
