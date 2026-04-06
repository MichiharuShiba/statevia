using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// 構造化ログ用。設定があれば最優先で <see cref="WorkflowEngine"/> が利用する。
    /// </summary>
    public ILogger<WorkflowEngine>? Logger { get; set; }

    /// <summary>
    /// <see cref="Logger"/> が null のとき、<see cref="Microsoft.Extensions.Logging.ILoggerFactory.CreateLogger{T}"/> で
    /// <see cref="WorkflowEngine"/> 用ロガーを生成する。両方 null なら <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/>。
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }
}
