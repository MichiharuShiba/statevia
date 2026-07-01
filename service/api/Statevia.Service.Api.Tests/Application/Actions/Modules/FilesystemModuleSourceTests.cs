using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Modules;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="FilesystemModuleSource"/> の単体テスト。</summary>
public sealed class FilesystemModuleSourceTests
{
    /// <summary>modules ルート配下の module ディレクトリと entry DLL を discover する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenModulePresent_ReturnsDiscoveredModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "sample-module");
        Directory.CreateDirectory(moduleDirectory);
        var dllPath = Path.Combine(moduleDirectory, "sample-module.dll");
        await File.WriteAllTextAsync(dllPath, "placeholder");

        var source = CreateSource(modulesRoot);

        // Act
        var discovered = await source.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Single(discovered);
        Assert.Equal("sample-module", discovered[0].ModuleDirectoryName);
        Assert.Equal(Path.GetFullPath(dllPath), discovered[0].EntryAssemblyPath);
        Assert.Equal("filesystem", discovered[0].SourceLabel);
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
