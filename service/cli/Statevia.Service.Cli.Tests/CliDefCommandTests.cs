using System.Net;
using Xunit;

namespace Statevia.Service.Cli.Tests;

/// <summary><c>def</c> サブコマンドの HttpListener テスト。</summary>
public sealed class CliDefCommandTests : IDisposable
{
    private readonly CliIsolatedHome _home = new();

    /// <inheritdoc />
    public void Dispose() => _home.Dispose();

    /// <summary><c>definition validate</c> は未知コマンドで非ゼロ。</summary>
    [Fact]
    public async Task DefinitionParent_IsUnknownCommand()
    {
        // Arrange
        var yamlPath = await CliCommandTestSupport.WriteTempYamlAsync(CliCommandTestSupport.ValidYaml);

        // Act
        var (exitCode, _, _) = await CliCommandTestSupport.RunAsync("definition", "validate", yamlPath);

        // Assert
        Assert.NotEqual(0, exitCode);
    }

    /// <summary>認証が無い API サブコマンドは login 案内で失敗する。</summary>
    [Fact]
    public async Task DefList_WithoutAuth_ReturnsFailureWithLoginHint()
    {
        // Act
        var (exitCode, _, stderr) = await CliCommandTestSupport.RunAsync(
            "def",
            "list",
            "--api-base",
            "http://127.0.0.1:8080",
            "--tenant",
            "acme-corp");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("auth login", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("Reload requires", stderr, StringComparison.Ordinal);
    }

    /// <summary>create は name と yaml を送り、2xx JSON を stdout に出す。</summary>
    [Fact]
    public async Task DefCreate_SendsNameAndYaml()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var yamlPath = await CliCommandTestSupport.WriteTempYamlAsync(CliCommandTestSupport.ValidYaml);
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.Created,
            """{"displayId":"wf-1"}""");

        // Act
        var (exitCode, stdout, _) = await CliCommandTestSupport.RunAsync(
            "def",
            "create",
            "--file",
            yamlPath,
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/v1/definitions", captured.Path);
        Assert.Equal("acme-corp", captured.TenantId);
        Assert.Contains("\"name\":\"cli-test\"", captured.Body, StringComparison.Ordinal);
        Assert.Contains("workflow:", captured.Body, StringComparison.Ordinal);
        Assert.Contains("wf-1", stdout, StringComparison.Ordinal);
    }

    /// <summary>--name は YAML 上の名前より優先する。</summary>
    [Fact]
    public async Task DefCreate_NameFlag_OverridesLoaderName()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var yamlPath = await CliCommandTestSupport.WriteTempYamlAsync(CliCommandTestSupport.ValidYaml);
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.Created,
            """{"displayId":"wf-1"}""");

        // Act
        var (exitCode, _, _) = await CliCommandTestSupport.RunAsync(
            "def",
            "create",
            "--file",
            yamlPath,
            "--name",
            "flag-name",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("\"name\":\"flag-name\"", captured.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"cli-test\"", captured.Body, StringComparison.Ordinal);
    }

    /// <summary>publish は PUT で name と yaml を送る。</summary>
    [Fact]
    public async Task DefPublish_SendsPutWithNameAndYaml()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var yamlPath = await CliCommandTestSupport.WriteTempYamlAsync(CliCommandTestSupport.ValidYaml);
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.OK,
            """{"displayId":"wf-1","latestVersion":2}""");

        // Act
        var (exitCode, _, _) = await CliCommandTestSupport.RunAsync(
            "def",
            "publish",
            "wf-1",
            "--file",
            yamlPath,
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("PUT", captured.Method);
        Assert.Equal("/v1/definitions/wf-1", captured.Path);
        Assert.Contains("\"name\":\"cli-test\"", captured.Body, StringComparison.Ordinal);
    }

    /// <summary>list は既定 limit=50 を付ける。</summary>
    [Fact]
    public async Task DefList_SendsDefaultLimit50()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"offset":0,"limit":50,"hasMore":false}""");

        // Act
        var (exitCode, _, _) = await CliCommandTestSupport.RunAsync(
            "def",
            "list",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("GET", captured.Method);
        Assert.Equal("/v1/definitions", captured.Path);
        Assert.Contains("limit=50", captured.Query, StringComparison.Ordinal);
        Assert.Contains("offset=0", captured.Query, StringComparison.Ordinal);
    }

    /// <summary>delete の 204 は stdout 空・終了 0。</summary>
    [Fact]
    public async Task DefDelete_NoContent_WritesEmptyStdout()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(listener, HttpStatusCode.NoContent, body: null);

        // Act
        var (exitCode, stdout, _) = await CliCommandTestSupport.RunAsync(
            "def",
            "delete",
            "wf-1",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("DELETE", captured.Method);
        Assert.Equal("/v1/definitions/wf-1", captured.Path);
        Assert.Equal(string.Empty, stdout);
    }

    /// <summary>422 は stderr に出し、API キーを stdout に出さない。</summary>
    [Fact]
    public async Task DefList_Unprocessable_WritesStderrWithoutLeakingApiKey()
    {
        // Arrange
        const string apiKey = "secret-api-key-must-not-print";
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.UnprocessableEntity,
            """{"error":"limit required"}""");

        // Act
        var (exitCode, stdout, stderr) = await CliCommandTestSupport.RunAsync(
            "def",
            "list",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--api-key",
            apiKey);
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Equal(apiKey, captured.ApiKey);
        Assert.Contains("limit required", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, stderr, StringComparison.Ordinal);
    }

    /// <summary>401 は login 案内を stderr に含む。</summary>
    [Fact]
    public async Task DefGet_Unauthorized_IncludesLoginHint()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.Unauthorized,
            """{"error":"unauthorized"}""");

        // Act
        var (exitCode, _, stderr) = await CliCommandTestSupport.RunAsync(
            "def",
            "get",
            "wf-1",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("auth login", stderr, StringComparison.Ordinal);
    }
}
