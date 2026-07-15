namespace Statevia.Infrastructure.Modules;

/// <summary>Git artifact 取得の抽象。HTTP archive API 依存を本契約に封じ込める。</summary>
/// <remarks>
/// <para>
/// <see cref="ResolveCommitShaAsync"/> は archive 本体を取得せず commit SHA のみ返す。
/// 呼び出し側がキャッシュ命中判定に使い、命中時は <see cref="FetchArchiveAsync"/> を省略できる。
/// </para>
/// </remarks>
internal interface IGitArtifactFetcher
{
    /// <summary>ref を commit SHA に解決する（archive は取得しない）。</summary>
    /// <param name="reference">取得対象の参照。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>解決済み commit SHA（40 桁 hex 想定）。</returns>
    Task<string> ResolveCommitShaAsync(GitModuleReference reference, CancellationToken cancellationToken);

    /// <summary>指定 commit のリポジトリ archive（zip）を取得する。</summary>
    /// <param name="reference">取得対象の参照。</param>
    /// <param name="commitSha">解決済み commit SHA。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>archive zip bytes。</returns>
    Task<byte[]> FetchArchiveAsync(
        GitModuleReference reference,
        string commitSha,
        CancellationToken cancellationToken);
}

/// <summary>Git Module artifact の参照。</summary>
/// <param name="Host">ホスト。</param>
/// <param name="Owner">オーナー。</param>
/// <param name="Repository">リポジトリ。</param>
/// <param name="Ref">branch / tag / commit。</param>
/// <param name="ModulePath">archive 内 Module パス。</param>
/// <param name="Provider">github / gitlab。</param>
/// <param name="Token">認証トークン（任意・機密）。</param>
/// <param name="PlainHttp">HTTP 接続するか。</param>
internal sealed record GitModuleReference(
    string Host,
    string Owner,
    string Repository,
    string Ref,
    string ModulePath,
    string Provider,
    string? Token,
    bool PlainHttp)
{
    /// <summary>可観測性用ラベル（機密は含めない）。</summary>
    public string Label => $"git:{Host}/{Owner}/{Repository}@{Ref}:{ModulePath}";
}

/// <summary>Git ホスト種別の定数。</summary>
internal static class GitModuleProviders
{
    /// <summary>GitHub（api.github.com / github.com）。</summary>
    public const string GitHub = "github";

    /// <summary>GitLab（gitlab.com および self-hosted GitLab API）。</summary>
    public const string GitLab = "gitlab";

    /// <summary>
    /// 明示 <paramref name="provider"/>、または既知 Host から正規化された provider 名を返す。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 推定できる Host は公開 SaaS のみ（<c>github.com</c> / <c>www.github.com</c> → github、
    /// <c>gitlab.com</c> / <c>www.gitlab.com</c> → gitlab）。
    /// self-hosted / Enterprise など未知 Host は推定せず、
    /// <c>Provider</c> に <c>github</c> または <c>gitlab</c> の明示指定を要求する。
    /// </para>
    /// </remarks>
    /// <param name="provider">明示 provider（任意）。</param>
    /// <param name="host">Git ホスト名。</param>
    /// <returns><see cref="GitHub"/> または <see cref="GitLab"/>。</returns>
    /// <exception cref="ArgumentException">未対応の provider、または Host から推定できない場合。</exception>
    public static string Resolve(string? provider, string host)
    {
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var normalized = provider.Trim().ToLowerInvariant();
            return normalized switch
            {
                GitHub => GitHub,
                GitLab => GitLab,
                _ => throw new ArgumentException($"Unsupported Git provider '{provider}'.", nameof(provider)),
            };
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var normalizedHost = NormalizeHost(host);
        return normalizedHost switch
        {
            "github.com" or "www.github.com" => GitHub,
            "gitlab.com" or "www.gitlab.com" => GitLab,
            _ => throw new ArgumentException(
                $"Unable to infer Git provider from host '{host}'. Set Provider to '{GitHub}' or '{GitLab}' explicitly.",
                nameof(host)),
        };
    }

    /// <summary>Host を小文字化し、末尾スラッシュとポートを除去する。</summary>
    private static string NormalizeHost(string host)
    {
        var normalized = host.Trim().TrimEnd('/').ToLowerInvariant();
        var colonIndex = normalized.LastIndexOf(':');
        // IPv6（[::1] 等）は想定外。hostname:port のみ剥がす。
        if (colonIndex > 0
            && !normalized.Contains(']', StringComparison.Ordinal)
            && int.TryParse(normalized.AsSpan(colonIndex + 1), out _))
        {
            return normalized[..colonIndex];
        }

        return normalized;
    }
}
