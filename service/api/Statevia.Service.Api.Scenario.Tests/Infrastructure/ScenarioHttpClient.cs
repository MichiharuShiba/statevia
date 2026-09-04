using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト用の HTTP ヘルパー。
/// </summary>
/// <remarks>
/// <para>ログイン・認証済みクライアント生成・ワークフロー状態ポーリング・エラー JSON 検証を提供する。</para>
/// <para>本番 Service を直接呼ばず、HTTP のみで Core-API と通信する。</para>
/// </remarks>
public sealed class ScenarioHttpClient(StateviaScenarioWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// ログインを実行し、アクセストークンを返す。
    /// </summary>
    /// <param name="tenantKey">外部テナントキー。</param>
    /// <param name="username">ユーザー名。</param>
    /// <param name="password">平文パスワード。</param>
    /// <returns>ログイン応答（アクセストークンを含む）。</returns>
    /// <exception cref="HttpRequestException">ログインが失敗した場合。</exception>
    public async Task<LoginResponse> LoginAsync(string tenantKey, string username, string password)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/login", new
        {
            tenantKey,
            username,
            password
        }, JsonOptions);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        return result!;
    }

    /// <summary>
    /// 認証済みの <see cref="HttpClient"/> を生成する。
    /// </summary>
    /// <param name="accessToken">JWT アクセストークン。</param>
    /// <param name="tenantKey">外部テナントキー（<c>X-Tenant-Id</c> ヘッダ）。</param>
    /// <returns>Bearer ヘッダと <c>X-Tenant-Id</c> が設定済みの <see cref="HttpClient"/>。</returns>
    public HttpClient CreateAuthenticatedClient(string accessToken, string tenantKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantKey);
        return client;
    }

    /// <summary>
    /// 実行ステータスを指定ステータスになるまでポーリングする。
    /// </summary>
    /// <param name="client">認証済み <see cref="HttpClient"/>。</param>
    /// <param name="executionDisplayId">実行 display ID。</param>
    /// <param name="expectedStatus">待機するステータス文字列（例: "Cancelled"）。</param>
    /// <param name="timeout">タイムアウト（既定 15 秒）。</param>
    /// <returns>最終取得した実行 JSON ドキュメント。</returns>
    /// <exception cref="TimeoutException">タイムアウト内に期待ステータスに達しない場合。</exception>
    public static async Task<JsonDocument> PollExecutionStatusAsync(
        HttpClient client,
        string executionDisplayId,
        string expectedStatus,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync(
                new Uri($"/v1/executions/{Uri.EscapeDataString(executionDisplayId)}", UriKind.Relative));
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();

            if (string.Equals(status, expectedStatus, StringComparison.OrdinalIgnoreCase))
                return doc;

            await Task.Delay(300);
        }

        throw new TimeoutException(
            $"実行 {executionDisplayId} が {timeout ?? TimeSpan.FromSeconds(15)} 以内に '{expectedStatus}' になりませんでした。");
    }

    /// <summary>
    /// HTTP 応答の error code を検証する。
    /// </summary>
    /// <remarks>
    /// レスポンス形式: <c>{ "error": { "code": "...", "message": "..." } }</c>
    /// </remarks>
    /// <param name="response">検証対象の HTTP 応答。</param>
    /// <param name="expectedCode">期待する error code 文字列。</param>
    public static async Task AssertErrorCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        ArgumentNullException.ThrowIfNull(response);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        string? code = null;
        if (doc.RootElement.TryGetProperty("error", out var errorEl) &&
            errorEl.TryGetProperty("code", out var codeEl))
        {
            code = codeEl.GetString();
        }
        Assert.Equal(expectedCode, code);
    }
}

/// <summary>ログイン応答（JSON デシリアライズ用）。</summary>
public sealed class LoginResponse
{
    /// <summary>JWT アクセストークン。</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>テナント外部キー。</summary>
    public string TenantKey { get; set; } = "";
}
