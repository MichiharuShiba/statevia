using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Api.Application.Definition;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Tests.Application.Actions.Execution;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

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

    /// <summary>同一 moduleId + version + action の重複登録時は skip する。</summary>
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
                ModuleId = "test.module",
                Version = "1.0.0",
                TrustLevel = ActionTrustLevel.Trusted,
                Source = ActionSourceKind.Builtin,
                Visibility = ActionVisibility.Builtin,
            },
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new StubState())));

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.True(host.Catalog.TryGetDescriptor("test.module", "1.0.0", "echo", out var descriptor));
        Assert.Equal(ActionTrustLevel.Trusted, descriptor!.TrustLevel);
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

    /// <summary>有効署名かつ信頼フィンガープリントの module は Verified で登録される。</summary>
    [Fact]
    public async Task LoadAsync_WhenValidSignatureTrusted_RegistersAsVerified()
    {
        // Arrange
        var modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
        var entryPath = Path.Combine(modulesRoot, "test.module", "test.module.dll");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa, signerName: "Statevia Official");
        var host = CreateHost(modulesRoot, new ModuleSigningOptions
        {
            TrustedSignerFingerprints = [ModuleSignatureTestHelper.ComputeFingerprint(rsa)],
        });

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.True(host.Catalog.TryGetDescriptor("test.module.echo", out var descriptor));
        Assert.Equal(ActionTrustLevel.Verified, descriptor!.TrustLevel);
        Assert.NotNull(descriptor.Signature);
        Assert.Equal("Statevia Official", descriptor.Signature!.SignerName);
    }

    /// <summary>有効署名だが未許可フィンガープリントの module は Signed で登録される。</summary>
    [Fact]
    public async Task LoadAsync_WhenValidSignatureUntrusted_RegistersAsSigned()
    {
        // Arrange
        var modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
        var entryPath = Path.Combine(modulesRoot, "test.module", "test.module.dll");
        using var rsa = ModuleSignatureTestHelper.CreateSigningKey();
        ModuleSignatureTestHelper.WriteSignatureFile(entryPath, rsa);
        var host = CreateHost(modulesRoot, new ModuleSigningOptions
        {
            TrustedSignerFingerprints = ["00DEADBEEF"],
        });

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.True(host.Catalog.TryGetDescriptor("test.module.echo", out var descriptor));
        Assert.Equal(ActionTrustLevel.Signed, descriptor!.TrustLevel);
        Assert.NotNull(descriptor.Signature);
    }

    /// <summary>RequireSignature=true で署名なし module は登録 skip される。</summary>
    [Fact]
    public async Task LoadAsync_WhenSignatureRequiredAndMissing_SkipsModule()
    {
        // Arrange
        var modulesRoot = CreateModuleLayoutFromBuiltAssembly("test.module");
        var host = CreateHost(modulesRoot, new ModuleSigningOptions { RequireSignature = true });

        // Act
        await host.Host.LoadAsync(OwnerTenantId, CancellationToken.None);

        // Assert
        Assert.False(host.Catalog.Exists("test.module.echo"));
        Assert.Equal(ModuleLoadStatus.Skipped, host.LoadCatalog.GetRecords().Single().Status);
    }

    private static TestModuleHost CreateHost(string modulesRoot, ModuleSigningOptions? signingOptions = null)
    {
        var catalog = new InMemoryActionCatalog();
        var loadCatalog = new ModuleLoadCatalog();
        var source = new FilesystemModuleSource(
            new StubModulePathProvider(modulesRoot),
            NullLogger<FilesystemModuleSource>.Instance);
        var verifier = new ModuleSignatureVerifier(
            Options.Create(signingOptions ?? new ModuleSigningOptions()),
            NullLogger<ModuleSignatureVerifier>.Instance);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var host = new ModuleHost(
            source,
            catalog,
            loadCatalog,
            verifier,
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
