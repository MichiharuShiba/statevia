using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.DependencyInjection;

/// <summary>
/// <see cref="IWorkflowEngine"/> をホストの <see cref="IServiceCollection"/> に登録する拡張。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// シングルトン <see cref="IWorkflowEngine"/> と共有 <see cref="IScheduler"/> を登録する。
    /// 事前に <see cref="IWorkflowInstanceIdGenerator"/> を登録すること（未登録時は解決失敗）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IWorkflowEngine"/> が Singleton のとき、同一プロセス内のワークフローは
    /// 同じ <see cref="IScheduler"/> インスタンスを共有する（グローバル並列制御の単一窓口）。
    /// </para>
    /// </remarks>
    public static IServiceCollection AddStateviaWorkflowEngine(
        this IServiceCollection services,
        Action<WorkflowEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new WorkflowEngineOptions();
        configure?.Invoke(options);

        services.AddSingleton<IScheduler>(_ => new DefaultScheduler(options.MaxParallelism));
        services.AddSingleton<IWorkflowInstanceFactory, DefaultWorkflowInstanceFactory>();
        services.AddSingleton<IWorkflowEngine>(sp => new WorkflowEngine(
            sp.GetRequiredService<IScheduler>(),
            sp.GetRequiredService<IWorkflowInstanceFactory>(),
            sp.GetRequiredService<IWorkflowInstanceIdGenerator>(),
            sp.GetRequiredService<ILogger<WorkflowEngine>>()));

        return services;
    }
}
