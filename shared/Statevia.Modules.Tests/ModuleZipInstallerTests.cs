using System.IO.Compression;
using Statevia.Modules;

namespace Statevia.Modules.Tests;

/// <summary><see cref="ModuleZipInstaller"/> の単体テスト。</summary>
public sealed class ModuleZipInstallerTests
{
    /// <summary>単一トップレベルディレクトリ zip を展開できる。</summary>
    [Fact]
    public void Install_WhenZipHasSingleTopLevelDirectory_ExtractsUnderModulesRoot()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "payload.zip");
        CreateZip(
            zipPath,
            ("test.module/test.module.dll", "MZ"u8.ToArray()),
            ("test.module/helper.dll", "MZ"u8.ToArray()));

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(modulesRoot, "test.module")), installed);
        Assert.Equal("MZ"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "test.module.dll")));
        Assert.Equal("MZ"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "helper.dll")));
    }

    /// <summary>ルート直下に複数ファイルがある zip は zip ベース名のディレクトリへ展開する。</summary>
    [Fact]
    public void Install_WhenZipHasFlatFiles_UsesZipBaseNameAsModuleDirectory()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "my.module.zip");
        CreateZip(
            zipPath,
            ("alpha.dll", "A"u8.ToArray()),
            ("beta.dll", "B"u8.ToArray()));

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(modulesRoot, "my.module")), installed);
        Assert.Equal("A"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "alpha.dll")));
        Assert.Equal("B"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "beta.dll")));
    }

    /// <summary>バックスラッシュ区切りの entry も展開できる。</summary>
    [Fact]
    public void Install_WhenZipUsesBackslashSeparators_ExtractsCorrectly()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "backslash.module.zip");
        CreateZip(zipPath, ("backslash.module\\nested\\entry.dll", "N"u8.ToArray()));

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.True(File.Exists(Path.Combine(installed, "nested", "entry.dll")));
    }

    /// <summary>modules ルートが無い場合は作成する。</summary>
    [Fact]
    public void Install_WhenModulesRootDoesNotExist_CreatesModulesRoot()
    {
        // Arrange
        var modulesRoot = Path.Combine(CreateTempDirectory(), "new-modules-root");
        var zipPath = Path.Combine(CreateTempDirectory(), "create-root.zip");
        CreateZip(zipPath, ("create.module/create.module.dll", "C"u8.ToArray()));

        // Act
        ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.True(Directory.Exists(modulesRoot));
        Assert.True(File.Exists(Path.Combine(modulesRoot, "create.module", "create.module.dll")));
    }

    /// <summary>既存 module ディレクトリは上書き展開する。</summary>
    [Fact]
    public void Install_WhenTargetDirectoryExists_ReplacesExistingContent()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var existing = Path.Combine(modulesRoot, "replace.module");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "stale.dll"), "old");
        var zipPath = Path.Combine(CreateTempDirectory(), "replace.module.zip");
        CreateZip(zipPath, ("replace.module/replace.module.dll", "new"u8.ToArray()));

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.Equal(Path.GetFullPath(existing), installed);
        Assert.False(File.Exists(Path.Combine(installed, "stale.dll")));
        Assert.Equal("new"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "replace.module.dll")));
    }

    /// <summary>zip が存在しない場合は FileNotFoundException。</summary>
    [Fact]
    public void Install_WhenZipFileMissing_ThrowsFileNotFound()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "missing.zip");

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
    }

    /// <summary>空の zip パスは ArgumentException。</summary>
    [Fact]
    public void Install_WhenZipPathIsBlank_ThrowsArgumentException()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ModuleZipInstaller.Install("  ", modulesRoot));
    }

    /// <summary>空の modules ルートは ArgumentException。</summary>
    [Fact]
    public void Install_WhenModulesRootIsBlank_ThrowsArgumentException()
    {
        // Arrange
        var zipPath = Path.Combine(CreateTempDirectory(), "arg.zip");
        CreateZip(zipPath, ("arg.module/arg.module.dll", "X"u8.ToArray()));

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ModuleZipInstaller.Install(zipPath, "  "));
    }

    /// <summary>空 zip は拒否する。</summary>
    [Fact]
    public void Install_WhenZipIsEmpty_Throws()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "empty.zip");
        CreateEmptyZip(zipPath);

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>トップレベルがパストラバーサルの entry は modules ルート外への展開を拒否する。</summary>
    [Fact]
    public void Install_WhenZipContainsTopLevelTraversal_Throws()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "evil.zip");
        CreateZip(zipPath, ("../escape.dll", "MZ"u8.ToArray()));

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
        Assert.Contains("escapes modules root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>ネストした .. セグメントは拒否する。</summary>
    [Fact]
    public void Install_WhenZipContainsParentDirectorySegment_Throws()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "nested-evil.zip");
        CreateZip(zipPath, ("nested.module/lib/../../escape.dll", "MZ"u8.ToArray()));

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
        Assert.Contains("invalid path segments", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>ドットセグメントを含む entry は拒否する。</summary>
    [Fact]
    public void Install_WhenZipContainsDotSegment_Throws()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "dot-segment.zip");
        CreateZip(zipPath, ("dot.module/./payload.dll", "MZ"u8.ToArray()));

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
        Assert.Contains("invalid path segments", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>32 MiB ちょうどの entry は展開できる。</summary>
    [Fact]
    public void Install_WhenZipEntryIsAtMaxEntrySize_ExtractsSuccessfully()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "max-entry.module.zip");
        CreateZipWithSizedEntry(zipPath, "max-entry.module/max-entry.module.dll", MaxEntryUncompressedBytes);

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.Equal(MaxEntryUncompressedBytes, new FileInfo(Path.Combine(installed, "max-entry.module.dll")).Length);
    }

    /// <summary>32 MiB を超える entry は拒否する。</summary>
    [Fact]
    public void Install_WhenZipEntryExceedsMaxEntrySize_Throws()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "oversized-entry.module.zip");
        CreateZipWithSizedEntry(zipPath, "oversized-entry.module/oversized-entry.module.dll", MaxEntryUncompressedBytes + 1);

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
        Assert.Contains("exceeds maximum allowed size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>展開後合計 64 MiB ちょうどの zip は展開できる。</summary>
    [Fact]
    public void Install_WhenZipArchiveIsAtMaxUncompressedSize_ExtractsSuccessfully()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "max-archive.module.zip");
        CreateZipWithSizedEntries(
            zipPath,
            ("max-archive.module/max-archive.module.dll", MaxArchiveUncompressedBytes / 2),
            ("max-archive.module/helper.dll", MaxArchiveUncompressedBytes / 2));

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        var totalExtractedBytes =
            new FileInfo(Path.Combine(installed, "max-archive.module.dll")).Length
            + new FileInfo(Path.Combine(installed, "helper.dll")).Length;
        Assert.Equal(MaxArchiveUncompressedBytes, totalExtractedBytes);
    }

    /// <summary>展開後合計が 64 MiB を超える zip は拒否する。</summary>
    [Fact]
    public void Install_WhenZipArchiveExceedsMaxUncompressedSize_Throws()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "oversized-archive.module.zip");
        var entrySizeBytes = (MaxArchiveUncompressedBytes / 3) + 1;
        CreateZipWithSizedEntries(
            zipPath,
            ("oversized-archive.module/oversized-archive.module.dll", entrySizeBytes),
            ("oversized-archive.module/helper-a.dll", entrySizeBytes),
            ("oversized-archive.module/helper-b.dll", entrySizeBytes));

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(() => ModuleZipInstaller.Install(zipPath, modulesRoot));
        Assert.Contains("exceeds maximum allowed uncompressed size", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>複数トップレベルディレクトリ zip は zip ベース名のディレクトリへ展開する。</summary>
    [Fact]
    public void Install_WhenZipHasMultipleTopLevelDirectories_UsesZipBaseNameAsModuleDirectory()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "multi.module.zip");
        CreateZip(
            zipPath,
            ("alpha/alpha.dll", "A"u8.ToArray()),
            ("beta/beta.dll", "B"u8.ToArray()));

        // Act
        var installed = ModuleZipInstaller.Install(zipPath, modulesRoot);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(modulesRoot, "multi.module")), installed);
        Assert.Equal("A"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "alpha", "alpha.dll")));
        Assert.Equal("B"u8.ToArray(), File.ReadAllBytes(Path.Combine(installed, "beta", "beta.dll")));
    }

    private const long MaxEntryUncompressedBytes = 32L * 1024 * 1024;
    private const long MaxArchiveUncompressedBytes = 64L * 1024 * 1024;

    private static void CreateZipWithSizedEntry(string zipPath, string entryName, long sizeBytes)
    {
        CreateZipWithSizedEntries(zipPath, (entryName, sizeBytes));
    }

    private static void CreateZipWithSizedEntries(string zipPath, params (string EntryName, long SizeBytes)[] entries)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var chunk = new byte[81920];
        foreach (var (entryName, sizeBytes) in entries)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
            using var stream = entry.Open();
            var remaining = sizeBytes;
            while (remaining > 0)
            {
                var writeSize = (int)Math.Min(remaining, chunk.Length);
                stream.Write(chunk, 0, writeSize);
                remaining -= writeSize;
            }
        }
    }

    private static void CreateEmptyZip(string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    }

    private static void CreateZip(string zipPath, params (string EntryName, byte[] Content)[] entries)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            using var stream = entry.Open();
            stream.Write(content);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-module-zip-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
