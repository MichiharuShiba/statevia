using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary><see cref="TenantBootstrapHostedService"/> の起動時テナント保証。</summary>
public sealed class TenantBootstrapHostedServiceTests
{
    /// <summary>StartAsync で既定テナントが存在しない場合に作成する。</summary>
    [Fact]
    public async Task StartAsync_WhenDefaultTenantMissing_CreatesTenant()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using (var db = database.Factory.CreateDbContext())
        {
            var tenants = await db.Tenants.IgnoreQueryFilters().ToListAsync();
            db.Tenants.RemoveRange(tenants);
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<CoreDbContext>>(database.Factory);
        services.AddScoped<IPlatformDataAccess, PlatformDataAccess>();
        services.AddSingleton<TenantBootstrapHostedService>();
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<TenantBootstrapHostedService>();

        // Act
        await hosted.StartAsync(CancellationToken.None);

        // Assert
        await using var scope = provider.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        var tenant = await platform.FindTenantByKeyAsync("default", CancellationToken.None);
        Assert.NotNull(tenant);
    }
}
