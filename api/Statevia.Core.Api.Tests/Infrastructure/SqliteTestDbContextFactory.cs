using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Tests.Infrastructure;

internal sealed class SqliteTestDbContextFactory : IDbContextFactory<CoreDbContext>
{
    private readonly DbContextOptions<CoreDbContext> _options;

    public SqliteTestDbContextFactory(DbContextOptions<CoreDbContext> options) => _options = options;

    public CoreDbContext CreateDbContext() => new CoreDbContext(_options);

    public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CoreDbContext(_options));
}

internal sealed class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }
    public IDbContextFactory<CoreDbContext> Factory { get; }
    public DbContextOptions<CoreDbContext> Options { get; }

    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        Options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlite(Connection)
            .Options;

        Factory = new SqliteTestDbContextFactory(Options);

        using var db = new CoreDbContext(Options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

