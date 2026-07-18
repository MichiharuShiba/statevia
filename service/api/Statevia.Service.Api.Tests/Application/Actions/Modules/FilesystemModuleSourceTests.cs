using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="FilesystemModuleSource"/> の単体テスト。</summary>
public sealed class FilesystemModuleSourceTests
{
    /// <summary>テナント配下レイアウトを discover する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenTenantScopedModule_ReturnsDiscoveredModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "acme-corp", "sample-module");
        Directory.CreateDirectory(moduleDirectory);
        var dllPath = Path.Combine(moduleDirectory, "sample-module.dll");
        await File.WriteAllTextAsync(dllPath, "placeholder");

        var source = CreateSource(modulesRoot);
        ModuleDiscoveryContext.TenantKey = "acme-corp";
        try
        {
            // Act
            var discovered = await source.DiscoverAsync(CancellationToken.None);

            // Assert
            Assert.Single(discovered);
            Assert.Equal("sample-module", discovered[0].ModuleDirectoryName);
            Assert.Equal(Path.GetFullPath(dllPath), discovered[0].EntryAssemblyPath);
            Assert.Equal("filesystem", discovered[0].SourceLabel);
        }
        finally
        {
            ModuleDiscoveryContext.Clear();
        }
    }

    /// <summary>テナントスコープ時、他テナント配下は列挙しない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenTenantScoped_IgnoresOtherTenantModules()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var ownDirectory = Path.Combine(modulesRoot, "acme-corp", "own.module");
        var otherDirectory = Path.Combine(modulesRoot, "other", "other.module");
        Directory.CreateDirectory(ownDirectory);
        Directory.CreateDirectory(otherDirectory);
        await File.WriteAllTextAsync(Path.Combine(ownDirectory, "own.module.dll"), "a");
        await File.WriteAllTextAsync(Path.Combine(otherDirectory, "other.module.dll"), "b");

        var source = CreateSource(modulesRoot);
        ModuleDiscoveryContext.TenantKey = "acme-corp";
        try
        {
            // Act
            var discovered = await source.DiscoverAsync(CancellationToken.None);

            // Assert
            Assert.Single(discovered);
            Assert.Equal("own.module", discovered[0].ModuleDirectoryName);
        }
        finally
        {
            ModuleDiscoveryContext.Clear();
        }
    }

    /// <summary>ルート直下の module 相当ディレクトリは discover しない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenModuleAtSharedRoot_Skips()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "sample-module");
        Directory.CreateDirectory(moduleDirectory);
        await File.WriteAllTextAsync(Path.Combine(moduleDirectory, "sample-module.dll"), "placeholder");

        var source = CreateSource(modulesRoot);

        // Act
        var discovered = await source.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>TenantKey 未設定時は discover しない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenTenantKeyUnset_ReturnsEmpty()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "default", "sample-module");
        Directory.CreateDirectory(moduleDirectory);
        await File.WriteAllTextAsync(Path.Combine(moduleDirectory, "sample-module.dll"), "placeholder");

        var source = CreateSource(modulesRoot);
        ModuleDiscoveryContext.Clear();

        // Act
        var discovered = await source.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>modules ルートが存在しない場合は空で返す。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenModulesRootMissing_ReturnsEmpty()
    {
        // Arrange
        var modulesRoot = Path.Combine(Path.GetTempPath(), "statevia-modules-test", Guid.NewGuid().ToString("N"));
        var source = CreateSource(modulesRoot);

        // Act
        var discovered = await source.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>単一 DLL のみ存在するとき entry として採用する。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenSingleDll_UsesSingleDll()
    {
        // Arrange
        var moduleDirectory = CreateTempDirectory();
        var dllPath = Path.Combine(moduleDirectory, "Only.dll");
        File.WriteAllText(dllPath, "placeholder");

        // Act
        var ok = FilesystemModuleSource.TryResolveEntryAssemblyPath(
            moduleDirectory,
            "other-name",
            out var resolved,
            out var reason);

        // Assert
        Assert.True(ok);
        Assert.Equal(Path.GetFullPath(dllPath), resolved);
        Assert.Empty(reason);
    }

    /// <summary>既定 Priority は中位値で、後方互換の基準点として固定されている。</summary>
    [Fact]
    public void Priority_ReturnsMidLevelDefault()
    {
        // Arrange
        var source = CreateSource(CreateTempDirectory());

        // Act
        var priority = source.Priority;

        // Assert
        Assert.Equal(100, priority);
    }

    /// <summary>複数 DLL がある場合は skip 理由を返す。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenMultipleDlls_Fails()
    {
        // Arrange
        var moduleDirectory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(moduleDirectory, "a.dll"), "a");
        File.WriteAllText(Path.Combine(moduleDirectory, "b.dll"), "b");

        // Act
        var ok = FilesystemModuleSource.TryResolveEntryAssemblyPath(
            moduleDirectory,
            "sample",
            out _,
            out var reason);

        // Assert
        Assert.False(ok);
        Assert.Contains("multiple", reason, StringComparison.OrdinalIgnoreCase);
    }

    private static FilesystemModuleSource CreateSource(string modulesRoot)
    {
        var provider = new StubModulePathProvider(modulesRoot);
        return new FilesystemModuleSource(provider, NullLogger<FilesystemModuleSource>.Instance);
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
