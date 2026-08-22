using System.Net;
using Xunit;

namespace Statevia.Service.Cli.Tests;

/// <summary><c>exec</c> サブコマンドの HttpListener テスト。</summary>
public sealed class CliExecCommandTests : IDisposable
{
    private readonly CliIsolatedHome _home = new();

    /// <inheritdoc />
    public void Dispose() => _home.Dispose();

    /// <summary><c>execution</c> 親は未知コマンドで非ゼロ。</summary>
    [Fact]
    public async Task ExecutionParent_IsUnknownCommand()
    {
        // Act
        var (exitCode, _, _) = await CliCommandTestSupport.RunAsync("execution", "list");

        // Assert
        Assert.NotEqual(0, exitCode);
    }

    /// <summary>start は definitionId と任意 input を送る。</summary>
    [Fact]
    public async Task ExecStart_SendsDefinitionIdAndInput()
    {
        // Arrange
        CliCommandTestSupport.SaveValidCredentials();
        var inputPath = await CliCommandTestSupport.WriteTempJsonAsync("""{"foo":"bar"}""");
        var port = CliCommandTestSupport.GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var listenerTask = CliCommandTestSupport.RespondOnceAsync(
            listener,
            HttpStatusCode.Created,
            """{"displayId":"ex-1","status":"Running"}""");

        // Act
        var (exitCode, stdout, _) = await CliCommandTestSupport.RunAsync(
            "exec",
            "start",
            "wf-1",
            "--version",
            "3",
            "--input",
            inputPath,
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/v1/executions", captured.Path);
        Assert.Contains("\"definitionId\":\"wf-1\"", captured.Body, StringComparison.Ordinal);
        Assert.Contains("\"definitionVersion\":3", captured.Body, StringComparison.Ordinal);
        Assert.Contains("\"foo\":\"bar\"", captured.Body, StringComparison.Ordinal);
        Assert.Contains("ex-1", stdout, StringComparison.Ordinal);
    }

    /// <summary>list は既定 limit=50 を付ける。</summary>
    [Fact]
    public async Task ExecList_SendsDefaultLimit50()
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
            "exec",
            "list",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("/v1/executions", captured.Path);
        Assert.Contains("limit=50", captured.Query, StringComparison.Ordinal);
    }

    /// <summary>cancel は POST .../cancel を呼ぶ。</summary>
    [Fact]
    public async Task ExecCancel_PostsCancelPath()
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
            "exec",
            "cancel",
            "ex-1",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp",
            "--idempotency-key",
            "cancel-1");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/v1/executions/ex-1/cancel", captured.Path);
        Assert.Equal("cancel-1", captured.IdempotencyKey);
        Assert.Equal(string.Empty, stdout);
    }

    /// <summary>resume は node パスと resumeKey を送る。</summary>
    [Fact]
    public async Task ExecResume_SendsResumeKey()
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
            """{"ok":true}""");

        // Act
        var (exitCode, _, _) = await CliCommandTestSupport.RunAsync(
            "exec",
            "resume",
            "ex-1",
            "--node",
            "abc123def456",
            "--event",
            "Approved",
            "--api-base",
            $"http://127.0.0.1:{port}",
            "--tenant",
            "acme-corp");
        var captured = await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/v1/executions/ex-1/nodes/abc123def456/resume", captured.Path);
        Assert.Contains("\"resumeKey\":\"Approved\"", captured.Body, StringComparison.Ordinal);
    }

    /// <summary>--node が .. のとき HTTP せず拒否する。</summary>
    [Fact]
    public async Task ExecResume_DotDotNode_ReturnsFailureWithoutHttp()
    {
        // Act
        var (exitCode, _, stderr) = await CliCommandTestSupport.RunAsync(
            "exec",
            "resume",
            "ex-1",
            "--node",
            "..",
            "--event",
            "Approved");

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("--node", stderr, StringComparison.Ordinal);
        Assert.Contains("path segment", stderr, StringComparison.Ordinal);
    }
}
