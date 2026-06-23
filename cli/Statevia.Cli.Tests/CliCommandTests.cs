using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Xunit;

namespace Statevia.Cli.Tests;

/// <summary>CLI コマンドのテスト。</summary>
public sealed class CliCommandTests
{
    /// <summary>definition validate が有効 YAML で成功する。</summary>
    [Fact]
    public async Task DefinitionValidate_ValidYaml_ReturnsSuccess()
    {
        // Arrange
        var yamlPath = await WriteTempYamlAsync(
            """
            workflow:
              name: cli-test
            states:
              start:
                on:
                  Completed:
                    end: true
            """);

        // Act
        var exitCode = await Program.Main(["definition", "validate", yamlPath]);

        // Assert
        Assert.Equal(0, exitCode);
    }

    /// <summary>definition validate は存在しないファイルで失敗する。</summary>
    [Fact]
    public async Task DefinitionValidate_MissingFile_ReturnsFailure()
    {
        // Act
        var exitCode = await Program.Main([
            "definition",
            "validate",
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.yaml"),
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>definition validate は不正 YAML で失敗する。</summary>
    [Fact]
    public async Task DefinitionValidate_InvalidYaml_ReturnsFailure()
    {
        // Arrange
        var yamlPath = await WriteTempYamlAsync("::::not-yaml::::");

        // Act
        var exitCode = await Program.Main(["definition", "validate", yamlPath]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>definition validate は検証エラーで失敗する。</summary>
    [Fact]
    public async Task DefinitionValidate_ValidationErrors_ReturnsFailure()
    {
        // Arrange
        var yamlPath = await WriteTempYamlAsync(
            """
            workflow:
              name: cli-test
            states:
              start:
                on:
                  Completed:
                    next: missing
            """);

        // Act
        var exitCode = await Program.Main(["definition", "validate", yamlPath]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>module install が zip を modules ルートへ展開する。</summary>
    [Fact]
    public async Task ModuleInstall_ValidZip_InstallsToModulesRoot()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
            "--skip-reload",
        ]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(modulesRoot, "test.module", "test.module.dll")));
    }

    /// <summary>module install は存在しない zip で失敗する。</summary>
    [Fact]
    public async Task ModuleInstall_MissingZip_ReturnsFailure()
    {
        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.zip"),
            "--skip-reload",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>module install は空 zip で失敗する。</summary>
    [Fact]
    public async Task ModuleInstall_EmptyZip_ReturnsFailure()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "empty.zip");
        CreateEmptyZip(zipPath);

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>api-base 未指定時は reload をスキップして成功する。</summary>
    [Fact]
    public async Task ModuleInstall_WithoutApiBase_ReturnsSuccess()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
        ]);

        // Assert
        Assert.Equal(0, exitCode);
    }

    /// <summary>reload には token が必須。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithoutToken_ReturnsFailure()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
            "--api-base",
            "http://localhost:8080",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>reload が成功すると 0 を返す。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadSuccess_ReturnsSuccess()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            Assert.Equal("POST", context.Request.HttpMethod);
            Assert.Equal("/internal/modules/reload", context.Request.Url?.AbsolutePath);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
        });

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--token",
            "test-token",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
    }

    /// <summary>reload が HTTP エラーのとき 1 を返す。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadHttpError_ReturnsFailure()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.Close();
        });

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--token",
            "test-token",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, exitCode);
    }

    /// <summary>reload は不正 api-base で失敗する。</summary>
    [Fact]
    public async Task ModuleInstall_InvalidApiBase_ReturnsFailure()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesRoot,
            "--api-base",
            "not-a-valid-uri",
            "--token",
            "test-token",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>definition validate はディレクトリ指定で失敗する。</summary>
    [Fact]
    public async Task DefinitionValidate_DirectoryPath_ReturnsFailure()
    {
        // Arrange
        var directoryPath = CreateTempDirectory();

        // Act
        var exitCode = await Program.Main(["definition", "validate", directoryPath]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>definition validate は空ファイルで失敗する。</summary>
    [Fact]
    public async Task DefinitionValidate_EmptyFile_ReturnsFailure()
    {
        // Arrange
        var yamlPath = await WriteTempYamlAsync(string.Empty);

        // Act
        var exitCode = await Program.Main(["definition", "validate", yamlPath]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>module install は modules パスがファイルのとき失敗する。</summary>
    [Fact]
    public async Task ModuleInstall_ModulesPathIsFile_ReturnsFailure()
    {
        // Arrange
        var modulesPathFile = Path.Combine(CreateTempDirectory(), "modules-file");
        await File.WriteAllTextAsync(modulesPathFile, "blocked");
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));

        // Act
        var exitCode = await Program.Main([
            "module",
            "install",
            zipPath,
            "--modules-path",
            modulesPathFile,
            "--skip-reload",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>definition validate はロック中ファイルで失敗する。</summary>
    [Fact]
    public async Task DefinitionValidate_LockedFile_ReturnsFailure()
    {
        // Arrange
        var yamlPath = Path.Combine(CreateTempDirectory(), "locked.yaml");
        await using (var stream = new FileStream(yamlPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            await stream.WriteAsync("workflow:\n  name: locked\n"u8.ToArray());

            // Act
            var exitCode = await Program.Main(["definition", "validate", yamlPath]);

            // Assert
            Assert.Equal(1, exitCode);
        }
    }

    private static async Task<string> WriteTempYamlAsync(string content)
    {
        var yamlPath = Path.Combine(Path.GetTempPath(), $"statevia-cli-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(yamlPath, content);
        return yamlPath;
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
        var path = Path.Combine(Path.GetTempPath(), "statevia-cli-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
