using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Application.Contracts.Security;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Api.Application.Actions.Modules;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="ModuleManagementService"/> の単体テスト。</summary>
public sealed class ModuleManagementServiceTests
{
    private static readonly Guid DefaultTenantGuid = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";

    /// <summary>Catalog レコードが自テナントのみ DTO にマッピングされる。</summary>
    [Fact]
    public void ListModules_FiltersByCurrentTenantOwner()
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
            EntryAssemblyPath = @"C:\modules\default\test.module\test.module.dll",
            OwnerTenantId = OwnerTenantId,
        });
        loadCatalog.Upsert(new ModuleLoadRecord
        {
            ModuleId = "other.module",
            Name = "Other",
            Version = "1.0.0",
            Status = ModuleLoadStatus.Loaded,
            Sha256 = "def",
            SourceLabel = "filesystem",
            LoadedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            Message = "ok",
            EntryAssemblyPath = @"C:\modules\acme\other.module\other.module.dll",
            OwnerTenantId = Guid.NewGuid().ToString("D"),
        });
        var service = CreateService(
            loadCatalog,
            ownerTenantId: null,
            tenantContext: CreateTenantContext(DefaultTenantGuid, "default"));

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

    /// <summary>テナント文脈があるとき、その UUID / tenant_key で reload する。</summary>
    [Fact]
    public async Task ReloadAsync_WhenTenantContextResolved_UsesCurrentTenant()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var service = CreateService(
            new ModuleLoadCatalog(),
            ownerTenantId: null,
            tenantContext: CreateTenantContext(tenantId, "acme-corp"));

        // Act
        var exception = await Record.ExceptionAsync(() => service.ReloadAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>OwnerTenantId 未設定かつテナント未解決時は既定テナントを DB から解決する。</summary>
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
            new FixedTenantContextAccessor(null),
            Options.Create(new ModuleHostOptions()));

        // Act
        var exception = await Record.ExceptionAsync(() => service.ReloadAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private static ModuleManagementService CreateService(
        ModuleLoadCatalog loadCatalog,
        string? ownerTenantId,
        ITenantContextAccessor? tenantContext = null) =>
        new(
            CreateModuleHost(),
            loadCatalog,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            tenantContext ?? CreateTenantContext(DefaultTenantGuid, "default"),
            Options.Create(new ModuleHostOptions { OwnerTenantId = ownerTenantId }));

    private static FixedTenantContextAccessor CreateTenantContext(Guid tenantId, string tenantKey) =>
        new(new TenantContextState(tenantId, tenantKey, PrincipalId: null, TenantLifecycle.Active));

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
