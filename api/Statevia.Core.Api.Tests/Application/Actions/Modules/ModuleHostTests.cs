using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Visibility;
using Statevia.Core.Api.Application.Actions.Catalog;
using Statevia.Core.Api.Application.Actions.Modules;
using Statevia.Core.Api.Application.Definition;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Tests.Application.Actions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;

namespace Statevia.Core.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="ModuleHost"/> の load / guard / isolation テスト。</summary>
public sealed class ModuleHostTests
{
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";

    /// <summary>discover → load → Catalog 登録が行える。</summary>
    [Fact]
    public async Task LoadAsync_WhenValidModule_RegistersActions()
    {
        // Arrange
        var modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
        var host = CreateHost(modulesRoot);

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.True(host.Catalog.Exists("test.module.echo"));
        Assert.True(host.Catalog.TryGetDescriptor("test.module.echo", out var descriptor));
        Assert.Equal(ActionVisibility.Tenant, descriptor!.Visibility);
        Assert.Equal(OwnerTenantId, descriptor.OwnerTenantId);
        Assert.Equal(ActionTrustLevel.Community, descriptor.TrustLevel);
        Assert.Equal(ActionSourceKind.Filesystem, descriptor.Source);
        Assert.NotNull(descriptor.Publisher);
        Assert.Equal("test.module", descriptor.Publisher!.PublisherId);
        Assert.True(host.Catalog.TryGetPublication("test.module.echo", out var publication));
        Assert.NotNull(publication);
        Assert.Equal("test.module.echo", publication!.Descriptor.ActionId);
        var loadRecord = host.LoadCatalog.GetRecords().Single();
        Assert.Equal(ModuleLoadStatus.Loaded, loadRecord.Status);
        Assert.NotNull(loadRecord.ModuleDescriptor);
        Assert.Equal("test.module.echo", Assert.Single(loadRecord.ModuleDescriptor!.ActionIds));
    }

    /// <summary>既存 actionId との衝突時は skip し Builtin を維持する。</summary>
    [Fact]
    public async Task LoadAsync_WhenDuplicateActionId_SkipsAndKeepsExisting()
    {
        // Arrange
        var modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
        var host = CreateHost(modulesRoot);
        host.Catalog.Register(
            new ActionDescriptor
            {
                ActionId = "test.module.echo",
                ModuleId = "existing.module",
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Trusted,
                Source = ActionSourceKind.Builtin,
                Visibility = ActionVisibility.Builtin,
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new StubState())));

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.True(host.Catalog.TryGetDescriptor("test.module.echo", out var descriptor));
        Assert.Equal("existing.module", descriptor!.ModuleId);
        Assert.Equal(ModuleLoadStatus.Duplicate, host.LoadCatalog.GetRecords().Single().Status);
    }

    /// <summary>壊れた module があっても他 module の load は継続する。</summary>
    [Fact]
    public async Task LoadAsync_WhenOneModuleFails_IsolatesFailure()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var brokenDirectory = Path.Combine(modulesRoot, "broken-module");
        Directory.CreateDirectory(brokenDirectory);
        await File.WriteAllTextAsync(Path.Combine(brokenDirectory, "broken-module.dll"), "not-a-dll");

        CreateModuleLayoutFromBuiltAssembly("test.module", modulesRoot);

        var host = CreateHost(modulesRoot);

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.True(host.Catalog.Exists("test.module.echo"));
        Assert.Contains(host.LoadCatalog.GetRecords(), record => record.Status == ModuleLoadStatus.Failed);
        Assert.Contains(host.LoadCatalog.GetRecords(), record => record.Status == ModuleLoadStatus.Loaded);
    }

    /// <summary>plugin action を参照する定義がコンパイルできる。</summary>
    [Fact]
    public async Task LoadAsync_WhenDefinitionUsesPluginAction_CompilesForOwnerTenant()
    {
        // Arrange
        var modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
        var host = CreateHost(modulesRoot);
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);
        DefinitionCompilerService.RegisterBuiltinActions(host.Catalog);

        var provider = ActionExecutionTestSupport.CreateProvider(
            host.Catalog,
            Guid.Parse(OwnerTenantId),
            tenantKey: "default");
        var compiler = new DefinitionCompilerService(
            host.Catalog,
            provider.GetRequiredService<IActionVisibilityResolver>(),
            new DefinitionLoadStrategy(
                new StateWorkflowDefinitionLoader(),
                new NodesWorkflowDefinitionLoader()),
            provider,
            NullLogger<DefinitionCompilerService>.Instance);
        const string yaml = """
            workflow:
              name: plugin-test
            states:
              start:
                action: test.module.echo
                on:
                  Completed:
                    end: true
            """;

        // Act
        var tenantId = Guid.Parse(OwnerTenantId);
        var (compiled, _) = compiler.ValidateAndCompile("plugin-test", yaml, tenantId);

        // Assert
        Assert.NotNull(compiled.StateExecutorFactory.GetExecutor("start"));
    }

    private static TestModuleHost CreateHost(string modulesRoot)
    {
        var catalog = new InMemoryActionCatalog();
        var loadCatalog = new ModuleLoadCatalog();
        var source = new FilesystemModuleSource(
            new StubModulePathProvider(modulesRoot),
            NullLogger<FilesystemModuleSource>.Instance);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var host = new ModuleHost(
            source,
            catalog,
            loadCatalog,
            provider,
            Options.Create(new ModuleHostOptions()),
            NullLogger<ModuleHost>.Instance);
        return new TestModuleHost(host, catalog, loadCatalog);
    }

    private static string CreateModuleLayoutFromBuiltAssembly(
        string moduleDirectoryName,
        string? modulesRoot = null)
    {
        modulesRoot ??= CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);

        var builtAssemblyPath = Path.Combine(AppContext.BaseDirectory, "TestActionModule.dll");
        var targetPath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll");
        File.Copy(builtAssemblyPath, targetPath, overwrite: true);

        var builtDeps = Directory.GetFiles(Path.GetDirectoryName(builtAssemblyPath)!, "*.dll");
        foreach (var dependency in builtDeps)
        {
            var dependencyName = Path.GetFileName(dependency);
            if (string.Equals(dependencyName, Path.GetFileName(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(dependency, Path.Combine(moduleDirectory, dependencyName), overwrite: true);
        }

        return modulesRoot;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-modules-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubState : IState<object?, object?>
    {
        public Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
            Task.FromResult<object?>(null);
    }

    private sealed class StubModulePathProvider(string modulesRoot) : IResolvedModulePathProvider
    {
        public string ModulesRoot { get; } = modulesRoot;
    }

    private sealed record TestModuleHost(ModuleHost Host, InMemoryActionCatalog Catalog, ModuleLoadCatalog LoadCatalog);
}
