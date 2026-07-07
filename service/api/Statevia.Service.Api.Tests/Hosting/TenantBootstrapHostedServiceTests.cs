using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Hosting;
using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Hosting;

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

        var services = BuildServices(database, devAdminEnabled: false);
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

    /// <summary>Development かつ DevAdmin 有効時に管理者を作成する。</summary>
    [Fact]
    public async Task StartAsync_WhenDevAdminEnabled_CreatesAdminUser()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var services = BuildServices(database, devAdminEnabled: true);
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetRequiredService<TenantBootstrapHostedService>();

        // Act
        await hosted.StartAsync(CancellationToken.None);

        // Assert
        await using var scope = provider.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        var login = await platform.FindLoginCredentialAsync("default", "admin", CancellationToken.None);
        Assert.NotNull(login);
        Assert.True(login.User.IsTenantAdmin);
    }

    private static ServiceCollection BuildServices(SqliteTestDatabase database, bool devAdminEnabled)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<CoreDbContext>>(database.Factory);
        services.AddScoped<IPlatformDataAccess, PlatformDataAccess>();
        services.AddSingleton<PasswordCredentialService>();
        services.AddScoped<TenantAdminBootstrap>();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(isDevelopment: true));
        services.AddSingleton<IOptions<DevAdminBootstrapOptions>>(
            Options.Create(new DevAdminBootstrapOptions
            {
                Enabled = devAdminEnabled,
                TenantKey = "default",
                Email = "admin",
                Password = "admin",
                DisplayName = "admin"
            }));
        services.AddSingleton<TenantBootstrapHostedService>();
        services.AddSingleton<ILogger<TenantBootstrapHostedService>>(NullLogger<TenantBootstrapHostedService>.Instance);
        return services;
    }

    private sealed class TestHostEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? Environments.Development : Environments.Production;
        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
