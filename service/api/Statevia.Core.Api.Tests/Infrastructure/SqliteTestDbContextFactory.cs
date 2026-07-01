using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Tests.Infrastructure;

internal sealed class SqliteTestDbContextFactory : IDbContextFactory<CoreDbContext>
{
    private readonly DbContextOptions<CoreDbContext> _options;
    private readonly SettableTenantContextAccessor _tenantAccessor;

    public SqliteTestDbContextFactory(
        DbContextOptions<CoreDbContext> options,
        SettableTenantContextAccessor tenantAccessor)
    {
        _options = options;
        _tenantAccessor = tenantAccessor;
    }

    public CoreDbContext CreateDbContext() =>
        new(_options, _tenantAccessor, DisabledTenantQueryFilterOptions.Instance);

    public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}

internal sealed class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }
    public SettableTenantContextAccessor TenantAccessor { get; }
    public IDbContextFactory<CoreDbContext> Factory { get; }
    public DbContextOptions<CoreDbContext> Options { get; }

    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        TenantAccessor = new SettableTenantContextAccessor();

        Options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlite(Connection)
            .Options;

        Factory = new SqliteTestDbContextFactory(Options, TenantAccessor);

        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
        SeedDefaultTenant(db);
        TenantAccessor.Set(TestTenantIds.DefaultContext);
    }

    private static void SeedDefaultTenant(CoreDbContext db)
    {
        if (db.Tenants.IgnoreQueryFilters().Any())
            return;

        var now = DateTime.UtcNow;
        db.Tenants.Add(new TenantRow
        {
            TenantId = TestTenantIds.DefaultTenantId,
            TenantKey = "default",
            DisplayName = "Default",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Tenants.Add(new TenantRow
        {
            TenantId = TestTenantIds.T1TenantId,
            TenantKey = "t1",
            DisplayName = "T1",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.SaveChanges();
    }

    public void Dispose() => Connection.Dispose();
}
