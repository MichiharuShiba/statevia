using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.Tests.TestSupport;

/// <summary>
/// テスト用に <see cref="WorkflowEngine"/> を既定の具象依存で組み立てるヘルパー。
/// </summary>
public static class WorkflowEngineTestHarness
{
    /// <summary>
    /// 既定の <see cref="DefaultScheduler"/>・<see cref="DefaultWorkflowInstanceFactory"/>・UUID v7 ID 生成でエンジンを生成する。
    /// </summary>
    /// <param name="maxParallelism"><see cref="DefaultScheduler"/> に渡す最大並列数。</param>
    /// <param name="logger">省略時は <see cref="NullLogger{WorkflowEngine}"/> を使用する。</param>
    /// <param name="workflowInstanceIdGenerator">省略時は <see cref="UuidV7WorkflowInstanceIdGenerator"/>。</param>
    /// <returns>組み立て済みの <see cref="WorkflowEngine"/>。</returns>
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
