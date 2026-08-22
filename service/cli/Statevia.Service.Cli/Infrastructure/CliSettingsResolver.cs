using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>フラグ・環境変数・ホーム config から CLI 設定値を合成する。</summary>
/// <remarks>優先順位はフラグ &gt; 環境変数 &gt; ホーム <c>config</c>。作業ディレクトリは見ない。</remarks>
/// <param name="configStore">非秘密ストア。省略時は環境のホーム。</param>
/// <param name="secretsStore">秘密ストア。省略時は環境のホーム。</param>
public sealed class CliSettingsResolver(
    CliConfigStore? configStore = null,
    CliSecretsStore? secretsStore = null)
{
    /// <summary><c>apiBase</c> を上書きする環境変数。</summary>
    internal const string ApiBaseEnvironmentVariable = "STATEVIA_API_BASE";

    /// <summary><c>tenant</c> を上書きする環境変数。</summary>
    internal const string TenantEnvironmentVariable = "STATEVIA_TENANT";

    private readonly CliConfigStore _configStore = configStore ?? new CliConfigStore();
    private readonly CliSecretsStore _secretsStore = secretsStore ?? new CliSecretsStore();

    /// <summary>Service API 基底 URL を解決する。</summary>
    /// <param name="flag"><c>--api-base</c>。</param>
    /// <returns>解決できた値。無ければ <see langword="null"/>。</returns>
    public string? ResolveApiBase(string? flag) =>
        FirstNonEmpty(flag, Environment.GetEnvironmentVariable(ApiBaseEnvironmentVariable), LoadConfig()?.ApiBase);

    /// <summary>テナントキーを解決する。</summary>
    /// <param name="flag"><c>--tenant</c>。</param>
    /// <returns>解決できた値。無ければ <see langword="null"/>。</returns>
    public string? ResolveTenant(string? flag) =>
        FirstNonEmpty(flag, Environment.GetEnvironmentVariable(TenantEnvironmentVariable), LoadConfig()?.Tenant);

    /// <summary>modules ルートを解決する。</summary>
    /// <param name="flag"><c>--modules-path</c>。</param>
    /// <returns>解決できた値。無ければ <see langword="null"/>（コマンド既定の <c>./modules</c> に委ねる）。</returns>
    public string? ResolveModulesPath(string? flag) =>
        FirstNonEmpty(
            flag,
            Environment.GetEnvironmentVariable(ModulePathResolver.EnvironmentVariable),
            LoadConfig()?.ModulesPath);

    /// <summary>API キーを解決する（フラグ / 環境変数 / ホーム secrets）。</summary>
    /// <param name="flag"><c>--api-key</c>。</param>
    /// <returns>解決できた値。無ければ <see langword="null"/>。</returns>
    public string? ResolveApiKey(string? flag) =>
        FirstNonEmpty(
            flag,
            Environment.GetEnvironmentVariable(CliApiAuthentication.ApiKeyEnvironmentVariable),
            _secretsStore.TryGetApiKey());

    private CliConfigFile? LoadConfig() => _configStore.TryLoad();

    private static string? FirstNonEmpty(string? first, string? second, string? third)
    {
        if (!string.IsNullOrWhiteSpace(first))
            return first.Trim();
        if (!string.IsNullOrWhiteSpace(second))
            return second.Trim();
        if (!string.IsNullOrWhiteSpace(third))
            return third.Trim();
        return null;
    }
}
