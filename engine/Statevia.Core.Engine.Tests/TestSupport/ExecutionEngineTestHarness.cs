using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.Tests.TestSupport;

/// <summary>
/// テスト用に <see cref="ExecutionEngine"/> を既定の具象依存で組み立てるヘルパー。
/// </summary>
internal static class ExecutionEngineTestHarness
{
    /// <summary>
    /// 既定の <see cref="DefaultScheduler"/>・<see cref="DefaultExecutionInstanceFactory"/>・UUID v7 ID 生成でエンジンを生成する。
    /// </summary>
    /// <param name="maxParallelism"><see cref="DefaultScheduler"/> に渡す最大並列数。</param>
    /// <param name="executionLogger">省略時は <see cref="NullLogger{TCategoryName}"/>（<see cref="ExecutionEngine.ExecutionEngineLogger"/>）を使用する。</param>
    /// <param name="executionIdGenerator">省略時は <see cref="UuidV7ExecutionIdGenerator"/>。</param>
    /// <returns>組み立て済みの <see cref="ExecutionEngine"/>。</returns>
    public static ExecutionEngine Create(
        int maxParallelism = 4,
        ILogger<ExecutionEngine.ExecutionEngineLogger>? executionLogger = null,
        IExecutionIdGenerator? executionIdGenerator = null)
    {
        executionLogger ??= NullLogger<ExecutionEngine.ExecutionEngineLogger>.Instance;
        executionIdGenerator ??= new UuidV7ExecutionIdGenerator();
        var scheduler = new DefaultScheduler(maxParallelism);
        var factory = new DefaultExecutionInstanceFactory();
        var loggerFactory = new SingleCategoryLoggerFactory<ExecutionEngine.ExecutionEngineLogger>(executionLogger);
        return new ExecutionEngine(scheduler, factory, executionIdGenerator, loggerFactory);
    }
}
