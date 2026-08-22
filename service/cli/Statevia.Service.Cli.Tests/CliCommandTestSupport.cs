using System.Net;
using System.Net.Sockets;
using System.Text;
using Statevia.Service.Cli.Infrastructure;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Statevia.Service.Cli.Tests;

/// <summary>CLI テストごとに <c>STATEVIA_HOME</c> と関連環境変数を隔離する。</summary>
internal sealed class CliIsolatedHome : IDisposable
{
    private readonly string? _previousCredentialsFile;
    private readonly string? _previousApiKey;
    private readonly string? _previousHome;
    private readonly string? _previousApiBase;
    private readonly string? _previousTenant;
    private readonly string? _previousModulesPath;
    private readonly string _isolatedHome;

    /// <summary>ホーム・資格情報・関連環境変数をテストごとに隔離する。</summary>
    public CliIsolatedHome()
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
}

/// <summary>HttpListener で受け取った 1 リクエスト。</summary>
/// <param name="Method">HTTP メソッド。</param>
/// <param name="Path">絶対パス。</param>
/// <param name="Query">クエリ文字列（先頭 <c>?</c> を含むことがある）。</param>
/// <param name="TenantId"><c>X-Tenant-Id</c>。</param>
/// <param name="ApiKey"><c>X-Api-Key</c>。</param>
/// <param name="Authorization"><c>Authorization</c>。</param>
/// <param name="IdempotencyKey"><c>X-Idempotency-Key</c>。</param>
/// <param name="Body">リクエスト本文。</param>
internal sealed record CapturedHttpRequest(
    string Method,
    string Path,
    string Query,
    string? TenantId,
    string? ApiKey,
    string? Authorization,
    string? IdempotencyKey,
    string Body);

/// <summary>CLI Main と HttpListener のテスト補助。</summary>
internal static class CliCommandTestSupport
{
    /// <summary>有効な最小 YAML。</summary>
    internal const string ValidYaml =
        """
        workflow:
          name: cli-test
        states:
          start:
            on:
              Completed:
                end: true
        """;

    /// <summary>stdout / stderr をキャプチャして <see cref="Program.Main"/> を実行する。</summary>
    /// <param name="args">CLI 引数。</param>
    /// <returns>終了コードと標準出力・標準エラー。</returns>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(params string[] args)
    {
        var previousOut = Console.Out;
        var previousErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            var exitCode = await Program.Main(args).ConfigureAwait(false);
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
        }
    }

    /// <summary>一時 YAML ファイルを書く。</summary>
    /// <param name="content">本文。</param>
    /// <returns>パス。</returns>
    public static async Task<string> WriteTempYamlAsync(string content)
    {
        var yamlPath = Path.Combine(Path.GetTempPath(), $"statevia-cli-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(yamlPath, content).ConfigureAwait(false);
        return yamlPath;
    }

    /// <summary>一時 JSON ファイルを書く。</summary>
    /// <param name="content">本文。</param>
    /// <returns>パス。</returns>
    public static async Task<string> WriteTempJsonAsync(string content)
    {
        var jsonPath = Path.Combine(Path.GetTempPath(), $"statevia-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(jsonPath, content).ConfigureAwait(false);
        return jsonPath;
    }

    /// <summary>空き TCP ポートを返す。</summary>
    /// <returns>ポート番号。</returns>
    public static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>1 リクエストを受けて固定応答を返す。</summary>
    /// <param name="listener">起動済みリスナー。</param>
    /// <param name="statusCode">応答ステータス。</param>
    /// <param name="body">応答本文。null なら本文なし。</param>
    /// <returns>受信内容。</returns>
    public static Task<CapturedHttpRequest> RespondOnceAsync(
        HttpListener listener,
        HttpStatusCode statusCode,
        string? body)
    {
        return Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            string requestBody;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);

            var captured = new CapturedHttpRequest(
                context.Request.HttpMethod,
                context.Request.Url?.AbsolutePath ?? string.Empty,
                context.Request.Url?.Query ?? string.Empty,
                context.Request.Headers["X-Tenant-Id"],
                context.Request.Headers["X-Api-Key"],
                context.Request.Headers["Authorization"],
                context.Request.Headers["X-Idempotency-Key"],
                requestBody);

            context.Response.StatusCode = (int)statusCode;
            if (body is not null)
            {
                context.Response.ContentType = "application/json";
                var bytes = Encoding.UTF8.GetBytes(body);
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            }

            context.Response.Close();
            return captured;
        });
    }

    /// <summary>有効な JWT をホーム credentials に書く。</summary>
    public static void SaveValidCredentials()
    {
        new CliCredentialsStore().Save(new CliCredentials(
            "acme-corp",
            "saved-access-token",
            DateTimeOffset.UtcNow.AddHours(1),
            ApiBase: null));
    }
}
