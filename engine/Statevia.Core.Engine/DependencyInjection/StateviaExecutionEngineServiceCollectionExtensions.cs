using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Scheduler;

namespace Statevia.Core.Engine.DependencyInjection;

/// <summary>
/// Core-API 等のホストが <see cref="IExecutionEngine"/> を DI に登録するための拡張メソッド。
/// </summary>
public static class StateviaExecutionEngineServiceCollectionExtensions
{
    /// <summary>
    /// シングルトン <see cref="IExecutionEngine"/> と共有 <see cref="IScheduler"/> を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <param name="configure">
    /// 登録前に適用する <see cref="ExecutionEngineOptions"/> のコールバック（例: 並列度の上書き）。
    /// <see langword="null"/> のときは既定の <see cref="ExecutionEngineOptions"/> を使用する。
    /// </param>
    /// <returns><paramref name="services"/>（メソッドチェーン用）。</returns>
    /// <remarks>
    /// <para>
    /// 本メソッドを呼ぶ前に、<see cref="IExecutionIdGenerator"/> を登録すること。
    /// 未登録の場合、<see cref="IExecutionEngine"/> の解決時に失敗する。
    /// </para>
    /// <para>
    /// <see cref="IExecutionEngine"/> を Singleton として登録する場合、同一プロセス内のワークフローは
    /// 同じ <see cref="IScheduler"/> インスタンスを共有する（グローバル並列制御の単一窓口）。
    /// </para>
    /// </remarks>
    public static IServiceCollection AddStateviaExecutionEngine(
        this IServiceCollection services,
        Action<ExecutionEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ExecutionEngineOptions();
        configure?.Invoke(options);

        ExecutionEngineServiceRegistrations.Register(services, options);

        return services;
    }
}
