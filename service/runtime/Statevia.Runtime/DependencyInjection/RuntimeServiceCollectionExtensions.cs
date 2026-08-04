using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Infrastructure.Common;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.DependencyInjection;
using Statevia.Runtime.Configuration;
using Statevia.Runtime.Services;

namespace Statevia.Runtime.DependencyInjection;

/// <summary>ランタイム用 hosted service の登録拡張。</summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Scheduler 専用ホスト向けに Persistence と DelayWait / OwnershipRecovery を登録する。
    /// </summary>
    /// <remarks>
    /// システム全体の wait / ownership をスキャンするためテナントフィルタは無効化する。
    /// 実行 Engine / Actions は登録しない。
    /// </remarks>
    public static IServiceCollection AddStateviaSchedulerHost(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = DatabaseConnection.Resolve(configuration);
        services.AddSingleton<ITenantQueryFilterOptions>(DisabledTenantQueryFilterOptions.Instance);
        services.AddSingleton<ITenantContextAccessor>(NullTenantContextAccessor.Instance);
        services.AddStateviaInfrastructurePersistence(connectionString);
        services.AddStateviaRuntimeOptions(configuration);
        services.AddStateviaRuntimeSchedulers();
        return services;
    }

    /// <summary>scheduler hosted service を登録する。</summary>
    public static IServiceCollection AddStateviaRuntimeSchedulers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHostedService<DelayWaitSchedulerHostedService>();
        services.AddHostedService<ExecutionOwnershipRecoveryHostedService>();
        return services;
    }

    /// <summary>worker hosted service を登録する。</summary>
    public static IServiceCollection AddStateviaRuntimeWorkerHostedService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHostedService<ExecutionWorkItemWorkerHostedService>();
        return services;
    }

    /// <summary>ランタイム設定をバインドし、起動時に検証する。</summary>
    public static IServiceCollection AddStateviaRuntimeOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<RuntimeOptions>()
            .Bind(configuration.GetSection(RuntimeOptions.SectionName))
            .Validate(
                static _ => true,
                "Statevia:Runtime must be bindable.")
            .ValidateOnStart();
        return services;
    }
}
