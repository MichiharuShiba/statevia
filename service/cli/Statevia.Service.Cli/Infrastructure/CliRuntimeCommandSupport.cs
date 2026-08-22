using System.CommandLine;
using System.CommandLine.Binding;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>解決済みの Runtime API 接続情報。</summary>
/// <param name="ApiBaseUri">Service API 基底 URL。</param>
/// <param name="TenantKey"><c>X-Tenant-Id</c>。</param>
/// <param name="Auth">Bearer または API キー。</param>
/// <param name="IdempotencyKey">非空白のときだけヘッダへ載せる。</param>
internal sealed record CliRuntimeSession(
    Uri ApiBaseUri,
    string TenantKey,
    CliApiAuth Auth,
    string? IdempotencyKey);

/// <summary>def / exec が共有するフラグとセッション解決。</summary>
internal static class CliRuntimeCommandSupport
{
    /// <summary><c>--api-base</c> / <c>-a</c>。</summary>
    /// <returns>新規 Option。</returns>
    public static Option<string?> CreateApiBaseOption() =>
        new(
            aliases: ["--api-base", "-a"],
            description: "Service API base URL. Falls back to STATEVIA_API_BASE or home config");

    /// <summary><c>--tenant</c> / <c>-t</c>。</summary>
    /// <returns>新規 Option。</returns>
    public static Option<string?> CreateTenantOption() =>
        new(
            aliases: ["--tenant", "-t"],
            description: "Tenant key. Falls back to STATEVIA_TENANT or home config");

    /// <summary><c>--api-key</c>。</summary>
    /// <returns>新規 Option。</returns>
    public static Option<string?> CreateApiKeyOption() =>
        new(
            "--api-key",
            "API key (X-Api-Key). Overrides STATEVIA_API_KEY and home secrets");

    /// <summary>ミューテーション用の任意 <c>--idempotency-key</c>。未指定時は自動生成しない。</summary>
    /// <returns>新規 Option。</returns>
    public static Option<string?> CreateIdempotencyKeyOption() =>
        new(
            "--idempotency-key",
            "Idempotency key header. Omitted when unset (not auto-generated)");

    /// <summary>一覧の <c>--limit</c>（既定 50）。</summary>
    /// <returns>新規 Option。</returns>
    public static Option<int> CreateLimitOption() =>
        new("--limit", () => 50, "Page size (1-500). Default 50");

    /// <summary>一覧の <c>--offset</c>（既定 0）。</summary>
    /// <returns>新規 Option。</returns>
    public static Option<int> CreateOffsetOption() =>
        new("--offset", () => 0, "Page offset (0 or greater). Default 0");

    /// <summary>パス 1 セグメントを検証・エンコードする。失敗時は stderr に書いて null。</summary>
    /// <param name="value">利用者入力。</param>
    /// <param name="parameterName">エラーに付けるパラメタ名（例: <c>id</c> / <c>--node</c>）。</param>
    /// <returns>エンコード済みセグメント。失敗時 null。</returns>
    public static async Task<string?> TryEncodePathSegmentAsync(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        if (!CliApiClient.TryEncodePathSegment(value, out var encoded, out var error))
        {
            await Console.Error.WriteLineAsync($"{parameterName}: {error}").ConfigureAwait(false);
            return null;
        }

        return encoded;
    }

    /// <summary>apiBase / tenant / 認証を解決する。失敗時は stderr に書いて null。</summary>
    /// <param name="apiBaseFlag"><c>--api-base</c>。</param>
    /// <param name="tenantFlag"><c>--tenant</c>。</param>
    /// <param name="apiKeyFlag"><c>--api-key</c>。</param>
    /// <param name="idempotencyKey"><c>--idempotency-key</c>。</param>
    /// <returns>成功時セッション。失敗時 null。</returns>
    public static async Task<CliRuntimeSession?> TryCreateSessionAsync(
        string? apiBaseFlag,
        string? tenantFlag,
        string? apiKeyFlag,
        string? idempotencyKey)
    {
        string resolvedApiBase;
        string resolvedTenant;
        try
        {
            var resolver = new CliSettingsResolver();
            resolvedApiBase = resolver.ResolveApiBase(apiBaseFlag) ?? string.Empty;
            resolvedTenant = resolver.ResolveTenant(tenantFlag) ?? string.Empty;
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return null;
        }

        if (string.IsNullOrWhiteSpace(resolvedApiBase))
        {
            await Console.Error
                .WriteLineAsync("--api-base is required (or STATEVIA_API_BASE / home config apiBase).")
                .ConfigureAwait(false);
            return null;
        }

        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            await Console.Error
                .WriteLineAsync("--tenant is required (or STATEVIA_TENANT / home config tenant).")
                .ConfigureAwait(false);
            return null;
        }

        if (!Uri.TryCreate(resolvedApiBase, UriKind.Absolute, out var apiBaseUri))
        {
            await Console.Error.WriteLineAsync("Invalid --api-base URL.").ConfigureAwait(false);
            return null;
        }

        CliApiAuth? auth;
        try
        {
            auth = CliApiAuthentication.Resolve(apiKeyFlag, new CliCredentialsStore(), out var error);
            if (auth is null)
            {
                await Console.Error.WriteLineAsync(error ?? "Authentication failed.").ConfigureAwait(false);
                return null;
            }
        }
        catch (CliHomeFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return null;
        }

        var trimmedIdempotency = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        return new CliRuntimeSession(apiBaseUri, resolvedTenant.Trim(), auth, trimmedIdempotency);
    }

    /// <summary>limit / offset を API 契約（1〜500 / 0 以上）で検証する。</summary>
    /// <param name="limit"><c>--limit</c>。</param>
    /// <param name="offset"><c>--offset</c>。</param>
    /// <returns>不正なら false。</returns>
    public static async Task<bool> ValidatePagingAsync(int limit, int offset)
    {
        if (limit is < 1 or > 500)
        {
            await Console.Error.WriteLineAsync("--limit must be between 1 and 500.").ConfigureAwait(false);
            return false;
        }

        if (offset < 0)
        {
            await Console.Error.WriteLineAsync("--offset must be 0 or greater.").ConfigureAwait(false);
            return false;
        }

        return true;
    }

    /// <summary>API を呼び、成功 JSON または失敗 stderr を書いて終了コードを返す。</summary>
    /// <param name="session">解決済みの接続情報。</param>
    /// <param name="request">メソッド・パス・任意の body / query / 冪等キー。</param>
    /// <returns>成功 0、失敗 1。</returns>
    public static async Task<int> SendAndWriteAsync(CliRuntimeSession session, CliApiSendRequest request)
    {
        try
        {
            var result = await CliApiClient.SendAsync(session, request).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
                return await CliApiClient.WriteFailureAsync(result).ConfigureAwait(false);

            await CliApiClient.WriteSuccessAsync(result).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await Console.Error.WriteLineAsync($"Request failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }
}

/// <summary>System.CommandLine の options record 束縛。</summary>
/// <typeparam name="T">束縛結果。</typeparam>
/// <param name="bind">ParseResult から record を作る。</param>
internal sealed class CliValueBinder<T>(Func<BindingContext, T> bind) : BinderBase<T>
{
    /// <inheritdoc />
    protected override T GetBoundValue(BindingContext bindingContext) => bind(bindingContext);
}
