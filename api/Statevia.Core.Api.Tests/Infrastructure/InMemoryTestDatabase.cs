using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Tests.Infrastructure;

internal sealed class InMemoryTestDbContextFactory : IDbContextFactory<CoreDbContext>
{
    private readonly DbContextOptions<CoreDbContext> _options;

    public InMemoryTestDbContextFactory(DbContextOptions<CoreDbContext> options) => _options = options;

    public CoreDbContext CreateDbContext() => new CoreDbContext(_options);

    public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CoreDbContext(_options));
}

internal sealed class InMemoryTestDatabase : IDisposable
{
    public DbContextOptions<CoreDbContext> Options { get; }
    public IDbContextFactory<CoreDbContext> Factory { get; }

    public InMemoryTestDatabase()
    {
        var dbName = "statevia-test-" + Guid.NewGuid();
        Options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Factory = new InMemoryTestDbContextFactory(Options);
    }

    public void Dispose()
    {
        // InMemory provider has no explicit dispose/cleanup requirement for isolated db names.
    }
}

