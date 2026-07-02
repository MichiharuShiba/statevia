using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Modules;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="ModuleManagementService"/> の単体テスト。</summary>
public sealed class ModuleManagementServiceTests
{
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";

    /// <summary>Catalog レコードが DTO にマッピングされる。</summary>
    [Fact]
    public void ListModules_MapsCatalogRecordsToDto()
    {
        // Arrange
        var loadCatalog = new ModuleLoadCatalog();
        loadCatalog.Upsert(new ModuleLoadRecord
        {
            ModuleId = "test.module",
            Name = "Test Module",
            Version = "1.0.0",
            Status = ModuleLoadStatus.Loaded,
            Sha256 = "abc",
            SourceLabel = "filesystem",
            LoadedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            Message = "ok",
            EntryAssemblyPath = @"C:\modules\test.module\test.module.dll",
        });
        var service = CreateService(loadCatalog, ownerTenantId: OwnerTenantId);

        // Act
        var modules = service.ListModules();

        // Assert
        var item = Assert.Single(modules);
        Assert.Equal("test.module", item.ModuleId);
        Assert.Equal("Loaded", item.Status);
        Assert.Equal("abc", item.Sha256);
    }

    /// <summary>OwnerTenantId 設定時は ModuleHost reload が成功する。</summary>
    [Fact]
    public async Task ReloadAsync_WhenOwnerTenantConfigured_Completes()
    {
        // Arrange
        var service = CreateService(new ModuleLoadCatalog(), ownerTenantId: OwnerTenantId);

        // Act
        var exception = await Record.ExceptionAsync(() => service.ReloadAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>OwnerTenantId 未設定時は既定テナントを DB から解決する。</summary>
    [Fact]
    public async Task ReloadAsync_WhenOwnerTenantNotConfigured_ResolvesDefaultTenant()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var services = new ServiceCollection();
        services.AddSingleton(database.Factory);
        services.AddScoped<IPlatformDataAccess, PlatformDataAccess>();
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var service = new ModuleManagementService(
            CreateModuleHost(),
            new ModuleLoadCatalog(),
            scopeFactory,
            Options.Create(new ModuleHostOptions()));

        // Act
        var exception = await Record.ExceptionAsync(() => service.ReloadAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private static ModuleManagementService CreateService(ModuleLoadCatalog loadCatalog, string? ownerTenantId) =>
        new(
            CreateModuleHost(),
            loadCatalog,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModuleHostOptions { OwnerTenantId = ownerTenantId }));

    private static ModuleHost CreateModuleHost()
    {
        var catalog = new InMemoryActionCatalog();
        var verifier = new ModuleSignatureVerifier(
            Options.Create(new ModuleSigningOptions()),
            NullLogger<ModuleSignatureVerifier>.Instance);
        return new ModuleHost(
            new EmptyModuleSource(),
            catalog,
            new ModuleLoadCatalog(),
            verifier,
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new ModuleHostOptions()),
            NullLogger<ModuleHost>.Instance);
    }

    private sealed class EmptyModuleSource : IModuleSource
    {
        /// <inheritdoc />
        public int Priority => 0;

        /// <inheritdoc />
        public Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiscoveredModule>>(Array.Empty<DiscoveredModule>());
    }
}
