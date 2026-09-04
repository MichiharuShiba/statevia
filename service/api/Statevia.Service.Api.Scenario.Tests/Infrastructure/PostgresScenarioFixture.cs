using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Testcontainers.PostgreSql;

namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト共有の Testcontainers PostgreSQL フィクスチャ。
/// </summary>
/// <remarks>
/// <para>Docker が利用可能な場合に PostgreSQL 16 コンテナを起動し、EF Core マイグレーション後に
/// <see cref="StateviaScenarioWebApplicationFactory"/> を構築する。</para>
/// <para>Docker 未検出時は <see cref="SkipReason"/> を設定し、各テストで <c>Skip.IfNot</c> を呼ぶことで
/// Skipped（Failed にならない）状態にする。</para>
/// </remarks>
public sealed class PostgresScenarioFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>Docker / コンテナが正常に初期化されているか。</summary>
    public bool IsReady { get; private set; }

    /// <summary>Docker 未検出またはコンテナ起動失敗時の Skip 理由。</summary>
    public string? SkipReason { get; private set; }

    /// <summary>PostgreSQL 接続文字列（<see cref="IsReady"/> が <c>true</c> の場合のみ有効）。</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>シナリオテスト用 <see cref="WebApplicationFactory"/>（<see cref="IsReady"/> が <c>true</c> の場合のみ有効）。</summary>
    public StateviaScenarioWebApplicationFactory? Factory { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine")
                .Build();

            await _container.StartAsync();

            ConnectionString = _container.GetConnectionString();
            Factory = new StateviaScenarioWebApplicationFactory(ConnectionString);

            // EF Core マイグレーションを適用し、既定テナントと権限カタログを保証する。
            // TenantBootstrapHostedService は除去しているため、カタログ未投入だと
            // is_tenant_admin ユーザーの ExpandPrincipalPermissionKeys が空集合になる。
            using var scope = Factory.Services.CreateScope();
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<CoreDbContext>>();
            await using var context = await dbFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();

            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
            await platform.EnsureDefaultTenantAsync(CancellationToken.None);
            await platform.EnsurePermissionCatalogAsync(CancellationToken.None);

            IsReady = true;
        }
        catch (Exception ex)
        {
            SkipReason = $"Testcontainers の起動に失敗したため、シナリオテストをスキップします: {ex.Message}";
            await Console.Error.WriteLineAsync($"WARNING: {SkipReason}");
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();
    }

}
