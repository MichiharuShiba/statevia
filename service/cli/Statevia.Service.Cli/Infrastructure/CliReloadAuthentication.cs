using System.Net.Http.Headers;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>module reload に使う認証ヘッダの解決結果。</summary>
/// <param name="BearerToken">Bearer トークン。API キー経路では <see langword="null"/>。</param>
/// <param name="ApiKey">API キー。Bearer 経路では <see langword="null"/>。</param>
/// <param name="WarnTokenDeprecated"><c>--token</c> を使ったとき true。</param>
internal sealed record CliReloadAuth(string? BearerToken, string? ApiKey, bool WarnTokenDeprecated);

/// <summary>module reload の認証を CLI オプションと資格情報ファイルから解決する。</summary>
internal static class CliReloadAuthentication
{
    internal const string ApiKeyEnvironmentVariable = "STATEVIA_API_KEY";

    /// <summary>
    /// 優先順位: <c>--api-key</c> / <c>STATEVIA_API_KEY</c> → ホーム secrets → 資格情報 JWT → <c>--token</c>（非推奨）。
    /// </summary>
    /// <param name="apiKeyOption"><c>--api-key</c>。</param>
    /// <param name="bearerTokenOption"><c>--token</c>。</param>
    /// <param name="store">資格情報ストア。</param>
    /// <param name="error">失敗時の利用者向けメッセージ。</param>
    /// <returns>解決できた認証。失敗時は <see langword="null"/>。</returns>
    public static CliReloadAuth? Resolve(
        string? apiKeyOption,
        string? bearerTokenOption,
        CliCredentialsStore store,
        out string? error)
    {
        error = null;
        var apiKey = FirstNonEmpty(apiKeyOption, Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable));
        if (!string.IsNullOrWhiteSpace(apiKey))
            return new CliReloadAuth(BearerToken: null, ApiKey: apiKey.Trim(), WarnTokenDeprecated: false);

        var homeApiKey = new CliSecretsStore().TryGetApiKey();
        if (!string.IsNullOrWhiteSpace(homeApiKey))
            return new CliReloadAuth(BearerToken: null, ApiKey: homeApiKey, WarnTokenDeprecated: false);

        if (store.TryGetValidAccessToken(out var accessToken) && !string.IsNullOrWhiteSpace(accessToken))
            return new CliReloadAuth(accessToken, ApiKey: null, WarnTokenDeprecated: false);

        if (!string.IsNullOrWhiteSpace(bearerTokenOption))
            return new CliReloadAuth(bearerTokenOption.Trim(), ApiKey: null, WarnTokenDeprecated: true);

        error = store.HasExpiredAccessToken()
            ? "Credentials expired. Run 'statevia auth login'."
            : "Reload requires --api-key (or STATEVIA_API_KEY), home secrets, a saved login (statevia auth login), or --token (deprecated).";
        return null;
    }

    /// <summary>解決済み認証を <see cref="HttpClient"/> へ載せる。</summary>
    public static void Apply(HttpClient client, CliReloadAuth auth)
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
