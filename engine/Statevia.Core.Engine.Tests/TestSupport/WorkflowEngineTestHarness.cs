using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.Tests.TestSupport;

/// <summary>
/// テスト用に <see cref="WorkflowEngine"/> を既定依存で組み立てる。
/// </summary>
public static class WorkflowEngineTestHarness
{
    /// <summary>
    /// 既定スケジューラ・既定ファクトリ・UUID v7 ID 生成でエンジンを生成する。
    /// </summary>
    public static WorkflowEngine Create(
        int maxParallelism = 4,
        ILogger<WorkflowEngine>? logger = null,
        IWorkflowInstanceIdGenerator? workflowInstanceIdGenerator = null)
    {
        logger ??= NullLogger<WorkflowEngine>.Instance;
        workflowInstanceIdGenerator ??= new UuidV7WorkflowInstanceIdGenerator();
        var scheduler = new DefaultScheduler(maxParallelism);
        var factory = new DefaultWorkflowInstanceFactory();
        return new WorkflowEngine(scheduler, factory, workflowInstanceIdGenerator, logger);
    }
}
