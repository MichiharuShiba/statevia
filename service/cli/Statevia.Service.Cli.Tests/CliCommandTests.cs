using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Statevia.Service.Cli.Infrastructure;
using Xunit;

namespace Statevia.Service.Cli.Tests;

/// <summary>CLI コマンドのテスト。</summary>
public sealed class CliCommandTests : IDisposable
{
    private readonly string? _previousCredentialsFile;
    private readonly string? _previousApiKey;
    private readonly string? _previousHome;
    private readonly string? _previousApiBase;
    private readonly string? _previousTenant;
    private readonly string? _previousModulesPath;
    private readonly string _isolatedHome;

    /// <summary>ホーム・資格情報・関連環境変数をテストごとに隔離する。</summary>
    public CliCommandTests()
    {
        _previousCredentialsFile = Environment.GetEnvironmentVariable("STATEVIA_CREDENTIALS_FILE");
        _previousApiKey = Environment.GetEnvironmentVariable("STATEVIA_API_KEY");
        _previousHome = Environment.GetEnvironmentVariable("STATEVIA_HOME");
        _previousApiBase = Environment.GetEnvironmentVariable("STATEVIA_API_BASE");
        _previousTenant = Environment.GetEnvironmentVariable("STATEVIA_TENANT");
        _previousModulesPath = Environment.GetEnvironmentVariable("STATEVIA_MODULES_PATH");
        _isolatedHome = Path.Combine(Path.GetTempPath(), "statevia-cli-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_isolatedHome);
        Environment.SetEnvironmentVariable("STATEVIA_HOME", _isolatedHome);
        Environment.SetEnvironmentVariable(
            "STATEVIA_CREDENTIALS_FILE",
            Path.Combine(_isolatedHome, "credentials"));
        Environment.SetEnvironmentVariable("STATEVIA_API_KEY", null);
        Environment.SetEnvironmentVariable("STATEVIA_API_BASE", null);
        Environment.SetEnvironmentVariable("STATEVIA_TENANT", null);
        Environment.SetEnvironmentVariable("STATEVIA_MODULES_PATH", null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("STATEVIA_CREDENTIALS_FILE", _previousCredentialsFile);
        Environment.SetEnvironmentVariable("STATEVIA_API_KEY", _previousApiKey);
        Environment.SetEnvironmentVariable("STATEVIA_HOME", _previousHome);
        Environment.SetEnvironmentVariable("STATEVIA_API_BASE", _previousApiBase);
        Environment.SetEnvironmentVariable("STATEVIA_TENANT", _previousTenant);
        Environment.SetEnvironmentVariable("STATEVIA_MODULES_PATH", _previousModulesPath);
        try
        {
            if (Directory.Exists(_isolatedHome))
                Directory.Delete(_isolatedHome, recursive: true);
        }
        catch (IOException)
        {
            // テスト隔離用ディレクトリの削除失敗は無視する。
        }
    }

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

    /// <summary>module install が zip をテナント配下へ展開する。</summary>
    [Fact]
    public async Task ModuleInstall_ValidZip_InstallsUnderTenantDirectory()
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
            "--tenant",
            "acme-corp",
            "--skip-reload",
        ]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(modulesRoot, "acme-corp", "test.module", "test.module.dll")));
        Assert.False(File.Exists(Path.Combine(modulesRoot, "test.module", "test.module.dll")));
    }

    /// <summary>--tenant 省略時は失敗する。</summary>
    [Fact]
    public async Task ModuleInstall_WithoutTenant_ReturnsFailure()
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
        Assert.NotEqual(0, exitCode);
    }

    /// <summary>不正な --tenant は展開前に失敗する。</summary>
    [Fact]
    public async Task ModuleInstall_InvalidTenant_ReturnsFailure()
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
            "--tenant",
            "..",
            "--skip-reload",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(modulesRoot));
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
            "--tenant",
            "default",
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
            "--tenant",
            "default",
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
            "--tenant",
            "default",
        ]);

        // Assert
        Assert.Equal(0, exitCode);
    }

    /// <summary>reload には API キー・資格情報・非推奨 --token のいずれかが必要。</summary>
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
            "--tenant",
            "default",
            "--api-base",
            "http://localhost:8080",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>reload 成功時、X-Tenant-Id は install の --tenant と一致する。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadSuccess_SendsMatchingTenantHeader()
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
            Assert.Equal("acme-corp", context.Request.Headers["X-Tenant-Id"]);
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
            "--tenant",
            "acme-corp",
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
            "--tenant",
            "default",
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
            "--tenant",
            "default",
            "--api-base",
            "not-a-valid-uri",
            "--token",
            "test-token",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>reload 成功時、非推奨の --token でも Bearer を送り警告を出す。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithDeprecatedToken_WritesWarning()
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
            Assert.Equal("Bearer test-token", context.Request.Headers["Authorization"]);
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
        });
        var previousError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        int exitCode;
        try
        {
            // Act
            exitCode = await Program.Main([
                "module",
                "install",
                zipPath,
                "--modules-path",
                modulesRoot,
                "--tenant",
                "default",
                "--api-base",
                $"http://127.0.0.1:{port}",
                "--token",
                "test-token",
            ]);
        }
        finally
        {
            Console.SetError(previousError);
        }

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
        Assert.Contains("deprecated", errorWriter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>--api-key 指定時は X-Api-Key で reload する。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithApiKey_SendsXApiKeyHeader()
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
            Assert.Equal("cli-api-key", context.Request.Headers["X-Api-Key"]);
            Assert.True(string.IsNullOrEmpty(context.Request.Headers["Authorization"]));
            Assert.Equal("acme-corp", context.Request.Headers["X-Tenant-Id"]);
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
            "--tenant",
            "acme-corp",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--api-key",
            "cli-api-key",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
    }

    /// <summary>STATEVIA_API_KEY でも reload できる。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithApiKeyEnvironmentVariable_SendsXApiKeyHeader()
    {
        // Arrange
        Environment.SetEnvironmentVariable("STATEVIA_API_KEY", "env-api-key");
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
            Assert.Equal("env-api-key", context.Request.Headers["X-Api-Key"]);
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
            "--tenant",
            "default",
            "--api-base",
            $"http://127.0.0.1:{port}",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
    }

    /// <summary>保存済み資格情報の access token で Bearer reload する。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithSavedCredentials_SendsBearerToken()
    {
        // Arrange
        new CliCredentialsStore().Save(new CliCredentials(
            "acme-corp",
            "saved-access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            ApiBase: null));
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
            Assert.Equal("Bearer saved-access-token", context.Request.Headers["Authorization"]);
            Assert.True(string.IsNullOrEmpty(context.Request.Headers["X-Api-Key"]));
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
            "--tenant",
            "acme-corp",
            "--api-base",
            $"http://127.0.0.1:{port}",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
    }

    /// <summary>期限切れ資格情報では reload せず再ログインを促す。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithExpiredCredentials_ReturnsFailure()
    {
        // Arrange
        new CliCredentialsStore().Save(new CliCredentials(
            "default",
            "expired-access-token",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            ApiBase: null));
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));
        var previousError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        int exitCode;
        try
        {
            // Act
            exitCode = await Program.Main([
                "module",
                "install",
                zipPath,
                "--modules-path",
                modulesRoot,
                "--tenant",
                "default",
                "--api-base",
                "http://127.0.0.1:1",
            ]);
        }
        finally
        {
            Console.SetError(previousError);
        }

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Credentials expired", errorWriter.ToString(), StringComparison.Ordinal);
    }

    /// <summary>auth login が資格情報ファイルを書き、トークンを標準出力に出さない。</summary>
    [Fact]
    public async Task AuthLogin_Success_WritesCredentialsFileWithoutPrintingToken()
    {
        // Arrange
        const string accessToken = "login-jwt-token-should-not-print";
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            Assert.Equal("POST", context.Request.HttpMethod);
            Assert.Equal("/v1/auth/login", context.Request.Url?.AbsolutePath);
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            Assert.Contains("acme-corp", requestBody, StringComparison.Ordinal);
            Assert.Contains("ops@example.com", requestBody, StringComparison.Ordinal);
            var responseJson =
                $$"""{"accessToken":"{{accessToken}}","expiresAt":"2099-12-31T00:00:00Z","tenantId":"11111111-1111-1111-1111-111111111111","tenantKey":"acme-corp","principalId":"22222222-2222-2222-2222-222222222222"}""";
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(responseBytes).ConfigureAwait(false);
            context.Response.Close();
        });
        var previousOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        int exitCode;
        try
        {
            // Act
            exitCode = await Program.Main([
                "auth",
                "login",
                "--api-base",
                $"http://127.0.0.1:{port}",
                "--tenant",
                "acme-corp",
                "--email",
                "ops@example.com",
                "--password",
                "secret-password",
            ]);
        }
        finally
        {
            Console.SetOut(previousOut);
        }

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
        var store = new CliCredentialsStore();
        Assert.True(File.Exists(store.FilePath));
        var loaded = store.TryLoad();
        Assert.NotNull(loaded);
        Assert.Equal(accessToken, loaded.AccessToken);
        Assert.Equal("acme-corp", loaded.TenantKey);
        Assert.DoesNotContain(accessToken, outWriter.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", outWriter.ToString(), StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            var fileMode = File.GetUnixFileMode(store.FilePath);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, fileMode);
        }
    }

    /// <summary>auth logout は資格情報ファイルを削除する。</summary>
    [Fact]
    public async Task AuthLogout_DeletesCredentialsFile()
    {
        // Arrange
        var store = new CliCredentialsStore();
        store.Save(new CliCredentials(
            "acme-corp",
            "to-be-deleted",
            DateTimeOffset.UtcNow.AddHours(1),
            ApiBase: null));
        Assert.True(File.Exists(store.FilePath));

        // Act
        var exitCode = await Program.Main(["auth", "logout"]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(store.FilePath));
    }

    /// <summary>auth login が HTTP 401 のとき失敗する。</summary>
    [Fact]
    public async Task AuthLogin_Unauthorized_ReturnsFailure()
    {
        // Arrange
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = RespondOnceAsync(listener, HttpStatusCode.Unauthorized, body: null);

        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--email",
            "ops@example.com",
            "--password",
            "wrong-password",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(new CliCredentialsStore().FilePath));
    }

    /// <summary>パスワード未入力は失敗する。</summary>
    [Fact]
    public async Task AuthLogin_EmptyPassword_ReturnsFailure()
    {
        // Arrange
        var previousIn = Console.In;
        Console.SetIn(new StringReader(string.Empty));

        int exitCode;
        try
        {
            // Act
            exitCode = await Program.Main([
                "auth",
                "login",
                "--api-base",
                "http://127.0.0.1:1",
                "--tenant",
                "acme-corp",
                "--email",
                "ops@example.com",
            ]);
        }
        finally
        {
            Console.SetIn(previousIn);
        }

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>プロンプト入力のパスワードでログインできる。</summary>
    [Fact]
    public async Task AuthLogin_PromptedPassword_WritesCredentials()
    {
        // Arrange
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = RespondOnceAsync(
            listener,
            HttpStatusCode.OK,
            """{"accessToken":"prompted-token","expiresAt":"2099-12-31T00:00:00Z","tenantId":"11111111-1111-1111-1111-111111111111","tenantKey":"acme-corp","principalId":"22222222-2222-2222-2222-222222222222"}""");
        var previousIn = Console.In;
        var previousOut = Console.Out;
        Console.SetIn(new StringReader("prompted-secret"));
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        int exitCode;
        try
        {
            // Act
            exitCode = await Program.Main([
                "auth",
                "login",
                "--api-base",
                $"http://127.0.0.1:{port}",
                "--tenant",
                "acme-corp",
                "--email",
                "ops@example.com",
            ]);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetOut(previousOut);
        }

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
        Assert.Equal("prompted-token", new CliCredentialsStore().TryLoad()?.AccessToken);
        Assert.DoesNotContain("prompted-secret", outWriter.ToString(), StringComparison.Ordinal);
    }

    /// <summary>不正な --api-base は失敗する。</summary>
    [Fact]
    public async Task AuthLogin_InvalidApiBase_ReturnsFailure()
    {
        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--api-base",
            "not-a-valid-uri",
            "--tenant",
            "acme-corp",
            "--email",
            "ops@example.com",
            "--password",
            "secret",
        ]);

        // Assert
        Assert.Equal(1, exitCode);
    }

    /// <summary>不正なログイン応答 JSON は失敗する。</summary>
    [Fact]
    public async Task AuthLogin_InvalidJson_ReturnsFailure()
    {
        // Arrange
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = RespondOnceAsync(listener, HttpStatusCode.OK, "{");

        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--email",
            "ops@example.com",
            "--password",
            "secret",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, exitCode);
    }

    /// <summary>accessToken 欠落の応答は失敗する。</summary>
    [Fact]
    public async Task AuthLogin_MissingAccessToken_ReturnsFailure()
    {
        // Arrange
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = RespondOnceAsync(
            listener,
            HttpStatusCode.OK,
            """{"accessToken":"","expiresAt":"2099-12-31T00:00:00Z","tenantId":"11111111-1111-1111-1111-111111111111","tenantKey":"acme-corp","principalId":"22222222-2222-2222-2222-222222222222"}""");

        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--email",
            "ops@example.com",
            "--password",
            "secret",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, exitCode);
    }

    /// <summary>既に期限切れのトークン応答は失敗する。</summary>
    [Fact]
    public async Task AuthLogin_ExpiredTokenInResponse_ReturnsFailure()
    {
        // Arrange
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = RespondOnceAsync(
            listener,
            HttpStatusCode.OK,
            """{"accessToken":"expired-jwt","expiresAt":"2000-01-01T00:00:00Z","tenantId":"11111111-1111-1111-1111-111111111111","tenantKey":"acme-corp","principalId":"22222222-2222-2222-2222-222222222222"}""");

        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--email",
            "ops@example.com",
            "--password",
            "secret",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(new CliCredentialsStore().FilePath));
    }

    /// <summary>応答の tenantKey が空なら要求のテナントキーを保存する。</summary>
    [Fact]
    public async Task AuthLogin_BlankTenantKey_UsesRequestedTenant()
    {
        // Arrange
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = RespondOnceAsync(
            listener,
            HttpStatusCode.OK,
            """{"accessToken":"blank-tenant-token","expiresAt":"2099-12-31T00:00:00Z","tenantId":"11111111-1111-1111-1111-111111111111","tenantKey":"","principalId":"22222222-2222-2222-2222-222222222222"}""");

        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--email",
            "ops@example.com",
            "--password",
            "secret",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
        Assert.Equal("acme-corp", new CliCredentialsStore().TryLoad()?.TenantKey);
    }

    /// <summary>不正 JSON の資格情報ファイルは未設定として扱う。</summary>
    [Fact]
    public async Task CliCredentialsStore_InvalidJson_ReturnsNull()
    {
        // Arrange
        var store = new CliCredentialsStore();
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(store.FilePath, "{");

        // Act
        var loaded = store.TryLoad();

        // Assert
        Assert.Null(loaded);
        Assert.False(store.TryGetValidAccessToken(out _));
        Assert.False(store.HasExpiredAccessToken());
    }

    /// <summary>明示パスのストアは環境変数と独立して読み書きする。</summary>
    [Fact]
    public void CliCredentialsStore_ExplicitPath_RoundTrips()
    {
        // Arrange
        var path = Path.Combine(CreateTempDirectory(), "explicit-credentials");
        var store = new CliCredentialsStore(path);

        // Act
        store.Save(new CliCredentials("acme-corp", "explicit-token", DateTimeOffset.UtcNow.AddHours(1), ApiBase: null));

        // Assert
        Assert.True(store.TryGetValidAccessToken(out var token));
        Assert.Equal("explicit-token", token);
        store.Delete();
        Assert.False(File.Exists(path));
    }

    /// <summary>空の accessToken は有効トークンとして扱わない。</summary>
    [Fact]
    public async Task CliCredentialsStore_BlankAccessToken_IsNotValid()
    {
        // Arrange
        var store = new CliCredentialsStore();
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(
            store.FilePath,
            """{"tenantKey":"acme","accessToken":"","expiresAt":"2099-12-31T00:00:00Z"}""");

        // Act
        var valid = store.TryGetValidAccessToken(out var token);

        // Assert
        Assert.False(valid);
        Assert.Null(token);
        Assert.False(store.HasExpiredAccessToken());
    }

    /// <summary>ロック中の資格情報ファイルは未設定として扱う。</summary>
    [Fact]
    public async Task CliCredentialsStore_LockedFile_ReturnsNull()
    {
        // Arrange
        var store = new CliCredentialsStore();
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await using (var stream = new FileStream(
            store.FilePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None))
        {
            await stream.WriteAsync("{}"u8.ToArray());

            // Act
            var loaded = store.TryLoad();

            // Assert
            Assert.Null(loaded);
        }
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
            "--tenant",
            "default",
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

    /// <summary>STATEVIA_HOME が credentials 既定パスをホーム配下へ寄せる。</summary>
    [Fact]
    public void CliHomePaths_StateviaHome_OverridesCredentialsDefaultPath()
    {
        // Arrange
        Environment.SetEnvironmentVariable("STATEVIA_CREDENTIALS_FILE", null);

        // Act
        var home = new CliHomePaths();
        var credentialsPath = CliCredentialsStore.ResolveDefaultPath();

        // Assert
        Assert.Equal(_isolatedHome, home.HomeDirectory);
        Assert.Equal(Path.Combine(_isolatedHome, "credentials"), credentialsPath);
        Assert.Equal(Path.Combine(_isolatedHome, "config"), home.ConfigPath);
        Assert.Equal(Path.Combine(_isolatedHome, "secrets"), home.SecretsPath);
    }

    /// <summary>設定解決はフラグが環境変数とホームより勝つ。</summary>
    [Fact]
    public void CliSettingsResolver_Flag_BeatsEnvironmentAndHome()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile("http://home.example/", "home-tenant", "./home-modules"));
        Environment.SetEnvironmentVariable("STATEVIA_API_BASE", "http://env.example/");
        Environment.SetEnvironmentVariable("STATEVIA_TENANT", "env-tenant");
        Environment.SetEnvironmentVariable("STATEVIA_MODULES_PATH", "./env-modules");
        var resolver = new CliSettingsResolver();

        // Act / Assert
        Assert.Equal("http://flag.example/", resolver.ResolveApiBase("http://flag.example/"));
        Assert.Equal("flag-tenant", resolver.ResolveTenant("flag-tenant"));
        Assert.Equal("./flag-modules", resolver.ResolveModulesPath("./flag-modules"));
    }

    /// <summary>設定解決は環境変数がホームより勝つ。</summary>
    [Fact]
    public void CliSettingsResolver_Environment_BeatsHome()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile("http://home.example/", "home-tenant", "./home-modules"));
        Environment.SetEnvironmentVariable("STATEVIA_API_BASE", "http://env.example/");
        Environment.SetEnvironmentVariable("STATEVIA_TENANT", "env-tenant");
        Environment.SetEnvironmentVariable("STATEVIA_MODULES_PATH", "./env-modules");
        var resolver = new CliSettingsResolver();

        // Act / Assert
        Assert.Equal("http://env.example/", resolver.ResolveApiBase(null));
        Assert.Equal("env-tenant", resolver.ResolveTenant(null));
        Assert.Equal("./env-modules", resolver.ResolveModulesPath(null));
    }

    /// <summary>フラグと環境変数が無いときホーム config を使う。</summary>
    [Fact]
    public void CliSettingsResolver_HomeConfig_UsedWhenFlagAndEnvironmentMissing()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile("http://home.example/", "home-tenant", "./home-modules"));
        var resolver = new CliSettingsResolver();

        // Act / Assert
        Assert.Equal("http://home.example/", resolver.ResolveApiBase(null));
        Assert.Equal("home-tenant", resolver.ResolveTenant(null));
        Assert.Equal("./home-modules", resolver.ResolveModulesPath(null));
    }

    /// <summary>API キー解決はフラグが環境変数とホーム secrets より勝つ。</summary>
    [Fact]
    public void CliSettingsResolver_ApiKeyFlag_BeatsEnvironmentAndHome()
    {
        // Arrange
        new CliSecretsStore().SaveApiKey("home-secret-key");
        Environment.SetEnvironmentVariable("STATEVIA_API_KEY", "env-secret-key");
        var resolver = new CliSettingsResolver();

        // Act / Assert
        Assert.Equal("flag-secret-key", resolver.ResolveApiKey("flag-secret-key"));
        Assert.Equal("env-secret-key", resolver.ResolveApiKey(null));

        Environment.SetEnvironmentVariable("STATEVIA_API_KEY", null);
        Assert.Equal("home-secret-key", resolver.ResolveApiKey(null));
    }

    /// <summary>config set/get/unset がホームに書き、未設定は (unset) になる。</summary>
    [Fact]
    public async Task ConfigSetGetUnset_WritesHomeAndPrintsUnset()
    {
        // Arrange
        var previousOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var setCode = await Program.Main(["config", "set", "tenant", "acme-corp"]);
            var getSetCode = await Program.Main(["config", "get", "tenant"]);
            var unsetCode = await Program.Main(["config", "unset", "tenant"]);
            var getUnsetCode = await Program.Main(["config", "get", "tenant"]);

            // Assert
            Assert.Equal(0, setCode);
            Assert.Equal(0, getSetCode);
            Assert.Equal(0, unsetCode);
            Assert.Equal(0, getUnsetCode);
            Assert.Contains("acme-corp", outWriter.ToString(), StringComparison.Ordinal);
            Assert.Contains("(unset)", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }

    /// <summary>config set の引数省略は各項目を尋ね、初期値を示して上書きできる。</summary>
    [Fact]
    public async Task ConfigSet_NoArgs_PromptsEachFieldAndOverwrites()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile("http://localhost:8080/", "old-tenant", "./old-modules"));
        new CliSecretsStore().SaveApiKey("old-secret-key");
        var previousIn = Console.In;
        var previousOut = Console.Out;
        Console.SetIn(new StringReader(
            "http://127.0.0.1:9090/\nnew-tenant\n./new-modules\nnew-secret-key\n"));
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "set"]);

            // Assert
            Assert.Equal(0, exitCode);
            var loaded = new CliConfigStore().TryLoad();
            Assert.NotNull(loaded);
            Assert.Equal("http://127.0.0.1:9090/", loaded.ApiBase);
            Assert.Equal("new-tenant", loaded.Tenant);
            Assert.Equal("./new-modules", loaded.ModulesPath);
            Assert.Equal("new-secret-key", new CliSecretsStore().TryGetApiKey());
            Assert.Contains("old-tenant", outWriter.ToString(), StringComparison.Ordinal);
            Assert.Contains("********", outWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("old-secret-key", outWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("new-secret-key", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetOut(previousOut);
        }
    }

    /// <summary>対話の空入力は既存値を維持する。</summary>
    [Fact]
    public async Task ConfigSet_NoArgs_EmptyInputKeepsExisting()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile("http://localhost:8080/", "keep-tenant", "./keep-modules"));
        new CliSecretsStore().SaveApiKey("keep-secret-key");
        var previousIn = Console.In;
        Console.SetIn(new StringReader("\n\n\n\n"));

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "set"]);

            // Assert
            Assert.Equal(0, exitCode);
            var loaded = new CliConfigStore().TryLoad();
            Assert.NotNull(loaded);
            Assert.Equal("http://localhost:8080/", loaded.ApiBase);
            Assert.Equal("keep-tenant", loaded.Tenant);
            Assert.Equal("./keep-modules", loaded.ModulesPath);
            Assert.Equal("keep-secret-key", new CliSecretsStore().TryGetApiKey());
        }
        finally
        {
            Console.SetIn(previousIn);
        }
    }

    /// <summary>キーだけ指定した set は現在値を初期値として尋ねる。</summary>
    [Fact]
    public async Task ConfigSet_KeyOnly_PromptsWithCurrentValue()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile(Tenant: "old-tenant"));
        var previousIn = Console.In;
        var previousOut = Console.Out;
        Console.SetIn(new StringReader("new-tenant\n"));
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "set", "tenant"]);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal("new-tenant", new CliConfigStore().TryLoad()?.Tenant);
            Assert.Contains("old-tenant", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetOut(previousOut);
        }
    }

    /// <summary>config set の API キー対話は平文を標準出力に出さない。</summary>
    [Fact]
    public async Task ConfigSet_ApiKeyPrompt_DoesNotPrintSecret()
    {
        // Arrange
        const string apiKey = "typed-secret-api-key";
        var previousIn = Console.In;
        var previousOut = Console.Out;
        Console.SetIn(new StringReader(apiKey + "\n"));
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "set", "api-key"]);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal(apiKey, new CliSecretsStore().TryGetApiKey());
            Assert.DoesNotContain(apiKey, outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetOut(previousOut);
        }
    }

    /// <summary>未知キーの set/get/unset は失敗する。</summary>
    [Fact]
    public async Task Config_UnknownKey_ReturnsFailure()
    {
        // Arrange
        var previousError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            // Act
            var setCode = await Program.Main(["config", "set", "unknown-key", "value"]);
            var getCode = await Program.Main(["config", "get", "unknown-key"]);
            var unsetCode = await Program.Main(["config", "unset", "unknown-key"]);

            // Assert
            Assert.Equal(1, setCode);
            Assert.Equal(1, getCode);
            Assert.Equal(1, unsetCode);
            Assert.Contains("Unknown key", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(previousError);
        }
    }

    /// <summary>不正な api-base は書き込まない。</summary>
    [Fact]
    public async Task ConfigSet_InvalidApiBase_ReturnsFailure()
    {
        // Arrange
        var previousError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "set", "api-base", "not-a-url"]);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains("Invalid api-base URL", errorWriter.ToString(), StringComparison.Ordinal);
            Assert.Null(new CliConfigStore().TryLoad()?.ApiBase);
        }
        finally
        {
            Console.SetError(previousError);
        }
    }

    /// <summary>対話の不正 api-base は既存値を残して失敗する。</summary>
    [Fact]
    public async Task ConfigSet_NoArgs_InvalidApiBase_KeepsExisting()
    {
        // Arrange
        new CliConfigStore().Save(new CliConfigFile("http://localhost:8080/", "keep-tenant", null));
        var previousIn = Console.In;
        var previousError = Console.Error;
        Console.SetIn(new StringReader("not-a-url\n"));
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "set"]);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Equal("keep-tenant", new CliConfigStore().TryLoad()?.Tenant);
        }
        finally
        {
            Console.SetIn(previousIn);
            Console.SetError(previousError);
        }
    }

    /// <summary>config unset api-key は secrets を削除する。</summary>
    [Fact]
    public async Task ConfigUnset_ApiKey_RemovesSecrets()
    {
        // Arrange
        new CliSecretsStore().SaveApiKey("to-unset");

        // Act
        var exitCode = await Program.Main(["config", "unset", "api-key"]);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Null(new CliSecretsStore().TryGetApiKey());
    }

    /// <summary>config get は API キーをマスクし、トークンを出さない。</summary>
    [Fact]
    public async Task ConfigGet_MasksApiKeyAndDoesNotPrintJwt()
    {
        // Arrange
        const string apiKey = "super-secret-api-key";
        new CliSecretsStore().SaveApiKey(apiKey);
        new CliCredentialsStore().Save(new CliCredentials(
            "acme-corp",
            "jwt-should-not-print",
            DateTimeOffset.UtcNow.AddHours(1),
            "http://localhost:8080/"));
        var previousOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "get"]);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("********", outWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(apiKey, outWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("jwt-should-not-print", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }

    /// <summary>不正なホーム config は非ゼロで、中身を出さない。</summary>
    [Fact]
    public async Task ConfigGet_InvalidJson_ReturnsFailureWithoutContents()
    {
        // Arrange
        File.WriteAllText(new CliHomePaths().ConfigPath, "{not-json");
        var previousError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            // Act
            var exitCode = await Program.Main(["config", "get", "tenant"]);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains("invalid JSON", errorWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("{not-json", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(previousError);
        }
    }

    /// <summary>auth login はフラグ省略時にホーム config を使い、成功時に書き戻す。</summary>
    [Fact]
    public async Task AuthLogin_WithoutApiBaseAndTenant_UsesHomeConfigAndWritesBack()
    {
        // Arrange
        const string accessToken = "login-from-home-config";
        var port = GetFreeTcpPort();
        new CliConfigStore().Save(new CliConfigFile($"http://127.0.0.1:{port}/", "acme-corp", null));
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            Assert.Contains("acme-corp", requestBody, StringComparison.Ordinal);
            var responseJson =
                $$"""{"accessToken":"{{accessToken}}","expiresAt":"2099-12-31T00:00:00Z","tenantId":"11111111-1111-1111-1111-111111111111","tenantKey":"acme-corp","principalId":"22222222-2222-2222-2222-222222222222"}""";
            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(responseBytes).ConfigureAwait(false);
            context.Response.Close();
        });

        // Act
        var exitCode = await Program.Main([
            "auth",
            "login",
            "--email",
            "ops@example.com",
            "--password",
            "secret-password",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
        var loaded = new CliConfigStore().TryLoad();
        Assert.NotNull(loaded);
        Assert.Equal("acme-corp", loaded.Tenant);
        Assert.StartsWith($"http://127.0.0.1:{port}/", loaded.ApiBase, StringComparison.Ordinal);
    }

    /// <summary>module install は --tenant 省略時にホーム config を使う。</summary>
    [Fact]
    public async Task ModuleInstall_WithoutTenantFlag_UsesHomeConfig()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));
        new CliConfigStore().Save(new CliConfigFile(Tenant: "acme-corp"));

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
        Assert.True(File.Exists(Path.Combine(modulesRoot, "acme-corp", "test.module", "test.module.dll")));
    }

    /// <summary>ホーム secrets の API キーで reload できる。</summary>
    [Fact]
    public async Task ModuleInstall_ReloadWithHomeSecrets_SendsXApiKeyHeader()
    {
        // Arrange
        const string apiKey = "home-secrets-api-key";
        new CliSecretsStore().SaveApiKey(apiKey);
        var modulesRoot = CreateTempDirectory();
        var zipPath = Path.Combine(CreateTempDirectory(), "test.module.zip");
        CreateZip(zipPath, ("test.module/test.module.dll", "MZ"u8.ToArray()));
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        string? receivedApiKey = null;
        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            receivedApiKey = context.Request.Headers["X-Api-Key"];
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
            "--tenant",
            "acme-corp",
            "--api-base",
            $"http://127.0.0.1:{port}",
        ]);

        // Assert
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, exitCode);
        Assert.Equal(apiKey, receivedApiKey);
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

    private static Task RespondOnceAsync(HttpListener listener, HttpStatusCode statusCode, string? body)
    {
        return Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            context.Response.StatusCode = (int)statusCode;
            if (body is not null)
            {
                context.Response.ContentType = "application/json";
                var bytes = Encoding.UTF8.GetBytes(body);
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            }

            context.Response.Close();
        });
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
