using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Infrastructure.Modules;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="ModuleLoadHostedService"/> の add-only watcher テスト。</summary>
public sealed class ModuleLoadHostedServiceTests
{
    /// <summary>起動後に新規 module 追加で load される。</summary>
    /// <remarks>
    /// watcher は Created 後に 500ms debounce してから load する。
    /// カバレッジ実行などで DLL コピーが遅いと固定 Delay では不足するため、条件成立までポーリングする。
    /// </remarks>
    [Fact]
    public async Task StartAsync_WhenNewModuleAdded_LoadsModule()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var modulesRoot = CreateTempDirectory();
        var catalog = new InMemoryActionCatalog();
        var loadCatalog = new ModuleLoadCatalog();
        var source = new FilesystemModuleSource(
            new StubModulePathProvider(modulesRoot),
            NullLogger<FilesystemModuleSource>.Instance);
        var services = new ServiceCollection();
        services.AddSingleton(database.Factory);
        services.AddScoped<IPlatformDataAccess, PlatformDataAccess>();
        using var provider = services.BuildServiceProvider();
        var verifier = new ModuleSignatureVerifier(
            Options.Create(new ModuleSigningOptions()),
            NullLogger<ModuleSignatureVerifier>.Instance);
        var moduleHost = new ModuleHost(
            source,
            catalog,
            loadCatalog,
            verifier,
            provider,
            Options.Create(new ModuleHostOptions()),
            NullLogger<ModuleHost>.Instance);
        var dependencies = new ModuleLoadHostedServiceDependencies(
            moduleHost,
            new StubModulePathProvider(modulesRoot));
        using var hostedService = new ModuleLoadHostedService(
            dependencies,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModuleHostOptions()),
            NullLogger<ModuleLoadHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        // Act
        CreateModuleLayoutFromBuiltAssembly("test.module", modulesRoot, tenantKey: "default");
        var loaded = await WaitUntilAsync(
            () => catalog.Exists("test.module.echo"),
            TimeSpan.FromSeconds(10));

        // Assert
        Assert.True(loaded);
        await hostedService.StopAsync(CancellationToken.None);
    }

    /// <summary>テナント配下パスから tenant_key を推定する。</summary>
    [Fact]
    public void TryResolveTenantKeyFromPath_WhenTenantLayout_ReturnsTenantKey()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var tenantDir = Path.Combine(modulesRoot, "acme-corp");
        Directory.CreateDirectory(tenantDir);

        // Act
        var key = ModuleLoadHostedService.TryResolveTenantKeyFromPath(
            modulesRoot,
            Path.Combine(tenantDir, "sample.module", "sample.module.dll"));

        // Assert
        Assert.Equal("acme-corp", key);
    }

    /// <summary>tenantKey 未満の浅いパスは無視する。</summary>
    [Fact]
    public void TryResolveTenantKeyFromPath_WhenShallowerThanTenantModule_ReturnsNull()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var tenantDir = Path.Combine(modulesRoot, "acme-corp");
        Directory.CreateDirectory(tenantDir);

        // Act
        var key = ModuleLoadHostedService.TryResolveTenantKeyFromPath(modulesRoot, tenantDir);

        // Assert
        Assert.Null(key);
    }

    /// <summary>条件が満たされるか、タイムアウトまで短間隔でポーリングする。</summary>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return condition();
    }

    private static void CreateModuleLayoutFromBuiltAssembly(
        string moduleDirectoryName,
        string modulesRoot,
        string tenantKey = "default")
    {
        var moduleDirectory = Path.Combine(modulesRoot, tenantKey, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);

        var builtAssemblyPath = Path.Combine(AppContext.BaseDirectory, "TestActionModule.dll");
        var targetPath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll");
        File.Copy(builtAssemblyPath, targetPath, overwrite: true);

        foreach (var dependency in Directory.GetFiles(Path.GetDirectoryName(builtAssemblyPath)!, "*.dll"))
        {
            var dependencyName = Path.GetFileName(dependency);
            if (string.Equals(dependencyName, Path.GetFileName(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(dependency, Path.Combine(moduleDirectory, dependencyName), overwrite: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-modules-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubModulePathProvider(string modulesRoot) : IResolvedModulePathProvider
    {
        public string ModulesRoot { get; } = modulesRoot;
    }
}
