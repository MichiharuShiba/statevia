using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>Runtime API への 1 回の送信内容（セッション以外）。</summary>
/// <param name="Method">HTTP メソッド。</param>
/// <param name="RelativePath">例: <c>v1/definitions</c>。先頭スラッシュなし。</param>
/// <param name="Query">任意クエリ。値が空の項目は送らない。</param>
/// <param name="Body">JSON 化するリクエスト本体。GET では null。</param>
/// <param name="IdempotencyKey">非空白のとき <c>X-Idempotency-Key</c>。</param>
internal sealed record CliApiSendRequest(
    HttpMethod Method,
    string RelativePath,
    IReadOnlyList<KeyValuePair<string, string?>>? Query = null,
    object? Body = null,
    string? IdempotencyKey = null);

/// <summary>Runtime API への 1 回の JSON 応答。</summary>
/// <param name="StatusCode">HTTP ステータス。</param>
/// <param name="Body">応答本文。無いときは空文字。</param>
/// <param name="IsSuccessStatusCode">2xx のとき true。</param>
internal sealed record CliApiSendResult(int StatusCode, string Body, bool IsSuccessStatusCode);

/// <summary>Service API へ相対 <c>v1/...</c> パスで JSON を送受信する。</summary>
/// <remarks>
/// <para>タイムアウトは <c>auth login</c> と同じ 30 秒。</para>
/// <para>ヘッダは <c>X-Tenant-Id</c> と認証（Bearer または <c>X-Api-Key</c>）。冪等キーは明示時のみ。</para>
/// </remarks>
internal static class CliApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>HTTP タイムアウト秒数。<c>auth login</c> と揃える。</summary>
    internal const int TimeoutSeconds = 30;

    /// <summary>
    /// URI パスの 1 セグメントとして使えるとき、Trim して <see cref="Uri.EscapeDataString"/> する。
    /// </summary>
    /// <remarks>
    /// <para><c>/</c> <c>\</c> やセグメント全体が <c>.</c> / <c>..</c>、制御文字は拒否する。
    /// <see cref="Uri"/> 結合時のドットセグメント正規化を防ぐ。</para>
    /// </remarks>
    /// <param name="value">利用者入力。</param>
    /// <param name="encoded">成功時のエンコード済みセグメント。</param>
    /// <param name="error">失敗時の理由（パラメタ名は含まない）。</param>
    /// <returns>使えるとき true。</returns>
    public static bool TryEncodePathSegment(string? value, out string encoded, out string? error)
    {
        encoded = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "is required.";
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed is "." or ".."
            || trimmed.Contains('/')
            || trimmed.Contains('\\')
            || trimmed.Any(static c => char.IsControl(c)))
        {
            error = "must be a single URL path segment (no '/', '\\', '.', '..', or control characters).";
            return false;
        }

        encoded = Uri.EscapeDataString(trimmed);
        return true;
    }

    /// <summary>相対パスへ JSON を送り、本文とステータスを返す。</summary>
    /// <param name="session">解決済みの接続情報。</param>
    /// <param name="request">メソッド・パス・任意の body / query / 冪等キー。</param>
    /// <returns>ステータスと本文。</returns>
    public static async Task<CliApiSendResult> SendAsync(CliRuntimeSession session, CliApiSendRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Method);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.TenantKey);

        var pathWithQuery = CombinePathAndQuery(request.RelativePath, request.Query);
        var requestUri = new Uri(session.ApiBaseUri, pathWithQuery);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        CliApiAuthentication.Apply(client, session.Auth);
        using var httpRequest = new HttpRequestMessage(request.Method, requestUri);
        httpRequest.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantKey);
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            httpRequest.Headers.TryAddWithoutValidation("X-Idempotency-Key", request.IdempotencyKey.Trim());

        if (request.Body is not null)
        {
            var json = JsonSerializer.Serialize(request.Body, JsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(httpRequest).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return new CliApiSendResult((int)response.StatusCode, responseBody, response.IsSuccessStatusCode);
    }

    /// <summary>成功時の JSON を stdout へ出す。204 や空本文は何も書かない。</summary>
    /// <param name="result">API 応答。</param>
    public static async Task WriteSuccessAsync(CliApiSendResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.StatusCode == 204 || string.IsNullOrWhiteSpace(result.Body))
            return;

        await Console.Out.WriteLineAsync(FormatJsonIfPossible(result.Body)).ConfigureAwait(false);
    }

    /// <summary>失敗時の本文を stderr へ出す。401 では login 案内を添える。</summary>
    /// <param name="result">API 応答。</param>
    /// <returns>常に 1。</returns>
    public static async Task<int> WriteFailureAsync(CliApiSendResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!string.IsNullOrWhiteSpace(result.Body))
            await Console.Error.WriteLineAsync(result.Body).ConfigureAwait(false);
        else
        {
            await Console.Error
                .WriteLineAsync($"Request failed: HTTP {result.StatusCode}.")
                .ConfigureAwait(false);
        }

        if (result.StatusCode == 401)
        {
            await Console.Error
                .WriteLineAsync("Run 'statevia auth login'.")
                .ConfigureAwait(false);
        }

        return 1;
    }

    private static string CombinePathAndQuery(
        string relativePath,
        IReadOnlyList<KeyValuePair<string, string?>>? query)
    {
        if (query is null || query.Count == 0)
            return relativePath;

        var pairs = query
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(static pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!.Trim())}")
            .ToArray();
        return pairs.Length == 0 ? relativePath : $"{relativePath}?{string.Join("&", pairs)}";
    }

    private static string FormatJsonIfPossible(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
