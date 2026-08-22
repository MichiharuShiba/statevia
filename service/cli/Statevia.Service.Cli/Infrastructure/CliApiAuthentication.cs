using System.Net.Http.Headers;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>Runtime API 呼び出しに使う認証ヘッダの解決結果。</summary>
/// <param name="BearerToken">Bearer トークン。API キー経路では <see langword="null"/>。</param>
/// <param name="ApiKey">API キー。Bearer 経路では <see langword="null"/>。</param>
internal sealed record CliApiAuth(string? BearerToken, string? ApiKey);

/// <summary>Runtime API 用の認証を CLI オプションとホーム資格情報から解決する。</summary>
/// <remarks>
/// <para>優先順位: <c>--api-key</c> / <c>STATEVIA_API_KEY</c> → ホーム secrets → 資格情報 JWT。</para>
/// <para><c>def</c> / <c>exec</c> と <c>module install</c> の reload で共用する。<c>--token</c> は無い。</para>
/// </remarks>
internal static class CliApiAuthentication
{
    /// <summary>API キーを上書きする環境変数。</summary>
    internal const string ApiKeyEnvironmentVariable = "STATEVIA_API_KEY";

    /// <summary>
    /// 優先順位: <c>--api-key</c> / <c>STATEVIA_API_KEY</c> → ホーム secrets → 資格情報 JWT。
    /// </summary>
    /// <param name="apiKeyOption"><c>--api-key</c>。</param>
    /// <param name="store">資格情報ストア。</param>
    /// <param name="error">失敗時の利用者向けメッセージ。</param>
    /// <returns>解決できた認証。失敗時は <see langword="null"/>。</returns>
    public static CliApiAuth? Resolve(
        string? apiKeyOption,
        CliCredentialsStore store,
        out string? error)
    {
        error = null;
        var apiKey = FirstNonEmpty(apiKeyOption, Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable));
        if (!string.IsNullOrWhiteSpace(apiKey))
            return new CliApiAuth(BearerToken: null, ApiKey: apiKey.Trim());

        var homeApiKey = new CliSecretsStore().TryGetApiKey();
        if (!string.IsNullOrWhiteSpace(homeApiKey))
            return new CliApiAuth(BearerToken: null, ApiKey: homeApiKey);

        if (store.TryGetValidAccessToken(out var accessToken) && !string.IsNullOrWhiteSpace(accessToken))
            return new CliApiAuth(accessToken, ApiKey: null);

        error = store.HasExpiredAccessToken()
            ? "Credentials expired. Run 'statevia auth login'."
            : "API calls require --api-key (or STATEVIA_API_KEY), home secrets, or a saved login (statevia auth login).";
        return null;
    }

    /// <summary>解決済み認証を <see cref="HttpClient"/> へ載せる。</summary>
    /// <param name="client">送信に使うクライアント。</param>
    /// <param name="auth">解決済み認証。</param>
    public static void Apply(HttpClient client, CliApiAuth auth)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(auth);
        if (!string.IsNullOrWhiteSpace(auth.ApiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", auth.ApiKey);
            return;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
            return first;
        if (!string.IsNullOrWhiteSpace(second))
            return second;
        return null;
    }
}
