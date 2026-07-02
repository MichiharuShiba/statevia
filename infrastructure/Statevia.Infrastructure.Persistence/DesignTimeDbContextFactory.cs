using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Statevia.Infrastructure.Persistence;

/// <summary>EF CLI 用 Design-time DbContext ファクトリ。</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    /// <inheritdoc />
    public CoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=statevia;Username=statevia;Password=statevia";

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new CoreDbContext(options, NullTenantContextAccessor.Instance, DisabledTenantQueryFilterOptions.Instance);
    }
}
