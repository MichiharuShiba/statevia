using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Bootstrap;

/// <summary>ブートストラップ CLI 用 DI 登録。</summary>
internal static class BootstrapServiceRegistration
{
    /// <summary>接続文字列を解決してサービスを登録する。</summary>
    public static ServiceProvider BuildProvider(BootstrapGlobalCliOptions? globalOptions = null) =>
        BuildProviderCore(registerNpgsql: true, dbContextFactory: null, globalOptions);

    /// <summary>テスト用: 既存の <see cref="IDbContextFactory{CoreDbContext}"/> でサービスを登録する。</summary>
    internal static ServiceProvider BuildProvider(IDbContextFactory<CoreDbContext> dbContextFactory) =>
        BuildProviderCore(registerNpgsql: false, dbContextFactory, globalOptions: null);

    private static ServiceProvider BuildProviderCore(
        bool registerNpgsql,
        IDbContextFactory<CoreDbContext>? dbContextFactory,
        BootstrapGlobalCliOptions? globalOptions)
    {
        var services = new ServiceCollection();
        if (registerNpgsql)
        {
            globalOptions ??= BootstrapGlobalCliOptions.Empty;
            var configuration = BootstrapConfiguration.Build(globalOptions);
            var connectionString = DatabaseConnection.Resolve(configuration, globalOptions.DatabaseUrl);
            services.AddDbContextFactory<CoreDbContext>(builder =>
                builder.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history")));
        }
        else
        {
            services.AddSingleton(dbContextFactory!);
        }

        AddBootstrapServices(services);
        return services.BuildServiceProvider();
    }

    private static void AddBootstrapServices(IServiceCollection services)
    {
        services.AddSingleton<ITenantContextAccessor>(NullTenantContextAccessor.Instance);
        services.AddSingleton<ITenantQueryFilterOptions>(DisabledTenantQueryFilterOptions.Instance);
        services.AddSingleton<PasswordCredentialService>();
        services.AddSingleton<IPlatformDataAccess, PlatformDataAccess>();
        services.AddSingleton<TenantBootstrap>();
        services.AddSingleton<TenantAdminBootstrap>();
    }
}
