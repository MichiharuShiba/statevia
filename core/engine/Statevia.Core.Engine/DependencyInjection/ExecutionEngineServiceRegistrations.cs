using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.DependencyInjection;

/// <summary>
/// <see cref="StateviaExecutionEngineServiceCollectionExtensions.AddStateviaExecutionEngine"/> が行う
/// 既定のサービス登録（スケジューラ・ファクトリ・エンジン）をまとめる。
/// </summary>
internal static class ExecutionEngineServiceRegistrations
{
    /// <summary>
    /// 解決済みの <see cref="ExecutionEngineOptions"/> に基づき、エンジン関連の Singleton を登録する。
    /// </summary>
    /// <param name="services">DI コンテナ。</param>
    /// <param name="options">並列度など、登録に用いるオプション。</param>
    internal static void Register(IServiceCollection services, ExecutionEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton<IScheduler>(_ => new DefaultScheduler(options.MaxParallelism));
        services.AddSingleton<IExecutionInstanceFactory, DefaultExecutionInstanceFactory>();
        services.AddSingleton<IExecutionEngine>(sp => new ExecutionEngine(
            sp.GetRequiredService<IScheduler>(),
            sp.GetRequiredService<IExecutionInstanceFactory>(),
            sp.GetRequiredService<IExecutionIdGenerator>(),
            sp.GetRequiredService<ILoggerFactory>()));
    }
}
