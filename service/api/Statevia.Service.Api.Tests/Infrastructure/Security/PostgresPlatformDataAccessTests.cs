using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Persistence.Repositories;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary>ローカル PostgreSQL 向けの手動スモーク（<c>STATEVIA_POSTGRES_SMOKE=1</c> 時のみ実行）。</summary>
public sealed class PostgresPlatformDataAccessTests
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=statevia;Username=statevia;Password=statevia";

  private static bool IsSmokeEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable("STATEVIA_POSTGRES_SMOKE"),
            "1",
            StringComparison.Ordinal);

    /// <summary>テナント管理者 Principal のグループ取得が PostgreSQL で例外にならない。</summary>
    [Fact]
    public async Task GetGroupSnapshotsForPrincipalAsync_TenantAdmin_DoesNotThrowOnPostgres()
    {
        if (!IsSmokeEnabled)
            return;

        // Arrange
        var principalId = Guid.Parse("e089ad26-41c2-4a9c-b30c-d0ad4d8340fb");
        var factory = CreatePostgresFactory();

        // Act
        var exception = await Record.ExceptionAsync(() =>
            new PlatformDataAccess(factory).GetGroupSnapshotsForPrincipalAsync(principalId, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>グループ所属ユーザーでも PostgreSQL でグループ取得が例外にならない。</summary>
    [Fact]
    public async Task GetGroupSnapshotsForPrincipalAsync_DevelopUserWithGroups_DoesNotThrowOnPostgres()
    {
        if (!IsSmokeEnabled)
            return;

        // Arrange
        var principalId = Guid.Parse("1e983106-b960-4ce7-a0dc-78b9e86243e3");
        var factory = CreatePostgresFactory();

        // Act
        var snapshots = await new PlatformDataAccess(factory)
            .GetGroupSnapshotsForPrincipalAsync(principalId, CancellationToken.None);

        // Assert
        Assert.NotEmpty(snapshots);
    }

    /// <summary>実 DB 上の定義で Start スナップショット構築が例外にならない。</summary>
    [Fact]
    public async Task CaptureForStartAsync_RealDefinition_DoesNotThrowOnPostgres()
    {
        if (!IsSmokeEnabled)
            return;

        // Arrange
        var tenantId = Guid.Parse("00000000-0000-4000-8000-000000000001");
        var principalId = Guid.Parse("e089ad26-41c2-4a9c-b30c-d0ad4d8340fb");
        var definitionId = await ResolveDefinitionIdAsync("e4oY6K4kXT");
        var database = new SqliteTestDatabase();
        database.TenantAccessor.Set(TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var postgresFactory = CreatePostgresFactory();
        var uowFactory = new TestCoreUnitOfWorkFactory(postgresFactory);
        var factory = new ExecutionSecuritySnapshotFactory(
            database.TenantAccessor,
            new PlatformDataAccess(postgresFactory),
            new TestCoreTransactionExecutor(uowFactory),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new ProjectRepository());

        // Act
        var exception = await Record.ExceptionAsync(() =>
            factory.CaptureForStartAsync(tenantId, definitionId, DateTime.UtcNow, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private static async Task<Guid> ResolveDefinitionIdAsync(string displayId)
    {
        var factory = CreatePostgresFactory();
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.DisplayIds
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(d => d.DisplayId == displayId && d.Kind == "definition");
        return row.ResourceId;
    }

    private static IDbContextFactory<CoreDbContext> CreatePostgresFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = DefaultConnectionString;
        else if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            connectionString = NormalizePostgresUrl(connectionString);

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PostgresDbContextFactory(options);
    }

    private sealed class PostgresDbContextFactory(DbContextOptions<CoreDbContext> options) : IDbContextFactory<CoreDbContext>
    {
        public CoreDbContext CreateDbContext() => new(options);

        public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static string NormalizePostgresUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        return $"Host={uri.Host};Port={uri.Port};Database={database};Username={user};Password={password}";
    }
}
