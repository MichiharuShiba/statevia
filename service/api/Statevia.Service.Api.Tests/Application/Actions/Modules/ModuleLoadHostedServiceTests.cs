using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="ModuleLoadHostedService"/> の add-only watcher テスト。</summary>
public sealed class ModuleLoadHostedServiceTests
{
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";

    /// <summary>起動後に新規 module 追加で load される。</summary>
    [Fact]
    public async Task StartAsync_WhenNewModuleAdded_LoadsModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var catalog = new InMemoryActionCatalog();
        var loadCatalog = new ModuleLoadCatalog();
        var source = new FilesystemModuleSource(
            new StubModulePathProvider(modulesRoot),
            NullLogger<FilesystemModuleSource>.Instance);
        var services = new ServiceCollection();
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
            Options.Create(new ModuleHostOptions { OwnerTenantId = OwnerTenantId }),
            NullLogger<ModuleHost>.Instance);
        var dependencies = new ModuleLoadHostedServiceDependencies(
            moduleHost,
            new StubModulePathProvider(modulesRoot));
        var hostedService = new ModuleLoadHostedService(
            dependencies,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ModuleHostOptions { OwnerTenantId = OwnerTenantId }),
            NullLogger<ModuleLoadHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        // Act
        CreateModuleLayoutFromBuiltAssembly("test.module", modulesRoot);
        await Task.Delay(800);

        // Assert
        Assert.True(catalog.Exists("test.module.echo"));
    }

    private static void CreateModuleLayoutFromBuiltAssembly(string moduleDirectoryName, string modulesRoot)
    {
        var moduleDirectory = Path.Combine(modulesRoot, moduleDirectoryName);
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
