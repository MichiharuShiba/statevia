using Microsoft.Extensions.Logging.Abstractions;
using Statevia.ActionHost.Modules;

namespace Statevia.ActionHost.Tests;

/// <summary><see cref="FilesystemModuleDiscoverer"/> の単体テスト。</summary>
public sealed class FilesystemModuleDiscovererTests
{
    /// <summary>存在しない modules ルートは空一覧を返す。</summary>
    [Fact]
    public void Discover_WhenModulesRootMissing_ReturnsEmpty()
    {
        // Arrange
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);
        var missingRoot = Path.Combine(Path.GetTempPath(), "statevia-action-host-test", Guid.NewGuid().ToString("N"));

        // Act
        var discovered = discoverer.Discover(missingRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>entry DLL が無いディレクトリはスキップされる。</summary>
    [Fact]
    public void Discover_WhenModuleDirectoryHasNoDll_SkipsDirectory()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(modulesRoot, "empty.module"));
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>複数 DLL があるディレクトリはスキップされる。</summary>
    [Fact]
    public void Discover_WhenMultipleDllsExist_SkipsDirectory()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "multi.module");
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, "a.dll"), "a");
        File.WriteAllText(Path.Combine(moduleDirectory, "b.dll"), "b");
        var discoverer = new FilesystemModuleDiscoverer(NullLogger<FilesystemModuleDiscoverer>.Instance);

        // Act
        var discovered = discoverer.Discover(modulesRoot, CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>単一 DLL のみのディレクトリは発見される。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenSingleDll_ReturnsPath()
    {
        // Arrange
        var moduleDirectory = CreateTempDirectory();
        var dllPath = Path.Combine(moduleDirectory, "only.dll");
        File.WriteAllText(dllPath, "dll");

        // Act
        var resolved = FilesystemModuleDiscoverer.TryResolveEntryAssemblyPath(
            moduleDirectory,
            "ignored",
            out var entryAssemblyPath,
            out var reason);

        // Assert
        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(dllPath), entryAssemblyPath);
        Assert.Empty(reason);
    }

    /// <summary>entry DLL が無い場合は失敗理由を返す。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenNoDll_ReturnsFailureReason()
    {
        // Arrange
        var moduleDirectory = CreateTempDirectory();

        // Act
        var resolved = FilesystemModuleDiscoverer.TryResolveEntryAssemblyPath(
            moduleDirectory,
            "missing.module",
            out var entryAssemblyPath,
            out var reason);

        // Assert
        Assert.False(resolved);
        Assert.Empty(entryAssemblyPath);
        Assert.Equal("no entry DLL found", reason);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-action-host-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
