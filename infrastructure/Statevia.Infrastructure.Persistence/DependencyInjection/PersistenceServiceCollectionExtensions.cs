using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Infrastructure.Persistence.Repositories;

namespace Statevia.Infrastructure.Persistence.DependencyInjection;

/// <summary>EF Core 永続化層の DI 登録。</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="CoreDbContext"/>、UoW、トランザクション実行、リポジトリ実装を登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <param name="connectionString">PostgreSQL 接続文字列。</param>
    public static IServiceCollection AddStateviaInfrastructurePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContextFactory<CoreDbContext>((_, options) =>
            options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddScoped<ICoreUnitOfWorkFactory, CoreUnitOfWorkFactory>();
        services.AddScoped<ICoreTransactionExecutor, CoreTransactionExecutor>();

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddScoped<IExecutionCursorRepository, ExecutionCursorRepository>();
        services.AddScoped<IExecutionWaitRepository, ExecutionWaitRepository>();
        services.AddScoped<ICommandDedupRepository, CommandDedupRepository>();
        services.AddScoped<IEventDeliveryDedupRepository, EventDeliveryDedupRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();

        return services;
    }
}
