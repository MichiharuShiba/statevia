using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト用の Core-API <see cref="WebApplicationFactory{TEntryPoint}"/>。
/// </summary>
/// <remarks>
/// <para>Testcontainers で起動した PostgreSQL 16 の接続文字列を注入し、
/// <see cref="TenantBootstrapHostedService"/> を除去する（MigrateAsync で既定テナントが投入済みのため）。</para>
/// <para>Cancel 後の投影更新のため、実行投影キューの HostedService は維持する。</para>
/// </remarks>
public sealed class StateviaScenarioWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Statevia.Service.Api.Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        // DATABASE_URL が設定されていると DefaultConnection より優先され、コンテナ以外へ接続する。
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

        builder.ConfigureServices(services =>
        {
            // TenantBootstrapHostedService は Fixture 側で Ensure* するため除去する。
            RemoveHostedService<TenantBootstrapHostedService>(services);
            // 開発マシンの別 PostgreSQL へ誤接続しないよう、定期 purge も除去する。
            RemoveHostedService<LoginFailureLockPurgeHostedService>(services);

            // ModuleHost にテナント ID を指定（DB lookup を回避）。
            services.Configure<ModuleHostOptions>(options =>
                options.OwnerTenantId = ScenarioTenantIds.DefaultTenantId.ToString("D"));
        });
    }

    /// <summary>指定型の <see cref="IHostedService"/> 登録を除去する。</summary>
    private static void RemoveHostedService<THostedService>(IServiceCollection services)
        where THostedService : class, IHostedService
    {
        var descriptor = services.SingleOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(THostedService));
        if (descriptor is not null)
            services.Remove(descriptor);
    }
}
