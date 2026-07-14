using System.Net.Http.Headers;
using System.Text.Json;

namespace Statevia.Infrastructure.Modules;

/// <summary>GitHub / GitLab の HTTP archive API を用いた <see cref="IGitArtifactFetcher"/> 実装。</summary>
/// <remarks>
/// <para>
/// LibGit2Sharp は使わず、ホストの REST API で commit 解決と zip archive 取得のみ行う。
/// 認証トークンはログへ出力しない。HttpClient は <see cref="IHttpClientFactory"/> 経由で取得し破棄しない。
/// 認証ヘッダはリクエスト単位で付与し、名前付きクライアントの共有状態を汚さない。
/// </para>
/// </remarks>
internal sealed class HttpGitArtifactFetcher(IHttpClientFactory httpClientFactory, ILogger<HttpGitArtifactFetcher> logger)
    : IGitArtifactFetcher
{
    /// <summary>名前付き HttpClient 名。</summary>
    public const string HttpClientName = "git-modules";

    /// <inheritdoc />
    public async Task<string> ResolveCommitShaAsync(
        GitModuleReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (LooksLikeFullCommitSha(reference.Ref))
        {
            return reference.Ref.ToLowerInvariant();
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var sha = reference.Provider switch
        {
            GitModuleProviders.GitHub => await ResolveGitHubCommitAsync(client, reference, cancellationToken)
                .ConfigureAwait(false),
            GitModuleProviders.GitLab => await ResolveGitLabCommitAsync(client, reference, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported Git provider '{reference.Provider}'."),
        };

        HttpGitArtifactFetcherLog.CommitResolved(logger, reference.Label, sha);
        return sha;
    }

    /// <inheritdoc />
    public async Task<byte[]> FetchArchiveAsync(
        GitModuleReference reference,
        string commitSha,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);

        var client = httpClientFactory.CreateClient(HttpClientName);
        var bytes = reference.Provider switch
        {
            GitModuleProviders.GitHub => await FetchGitHubArchiveAsync(client, reference, commitSha, cancellationToken)
                .ConfigureAwait(false),
            GitModuleProviders.GitLab => await FetchGitLabArchiveAsync(client, reference, commitSha, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported Git provider '{reference.Provider}'."),
        };

        HttpGitArtifactFetcherLog.ArchiveFetched(logger, reference.Label, commitSha);
        return bytes;
    }

    private static string BaseUrl(GitModuleReference reference)
    {
        var scheme = reference.PlainHttp ? "http" : "https";
        return $"{scheme}://{reference.Host.TrimEnd('/')}";
    }

    private static async Task<string> ResolveGitHubCommitAsync(
        HttpClient client,
        GitModuleReference reference,
        CancellationToken cancellationToken)
    {
        // github.com の API は api.github.com。Enterprise は {host}/api/v3 を想定。
        var apiRoot = reference.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? "https://api.github.com"
            : $"{BaseUrl(reference)}/api/v3";
        var url =
            $"{apiRoot}/repos/{Uri.EscapeDataString(reference.Owner)}/{Uri.EscapeDataString(reference.Repository)}/commits/{Uri.EscapeDataString(reference.Ref)}";
        using var request = CreateRequest(HttpMethod.Get, url, reference);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("sha", out var shaElement))
        {
            throw new InvalidOperationException($"GitHub commit response missing 'sha' for '{reference.Label}'.");
        }

        return shaElement.GetString()
            ?? throw new InvalidOperationException($"GitHub commit sha was null for '{reference.Label}'.");
    }

    private static async Task<byte[]> FetchGitHubArchiveAsync(
        HttpClient client,
        GitModuleReference reference,
        string commitSha,
        CancellationToken cancellationToken)
    {
        var apiRoot = reference.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? "https://api.github.com"
            : $"{BaseUrl(reference)}/api/v3";
        var url =
            $"{apiRoot}/repos/{Uri.EscapeDataString(reference.Owner)}/{Uri.EscapeDataString(reference.Repository)}/zipball/{Uri.EscapeDataString(commitSha)}";
        using var request = CreateRequest(HttpMethod.Get, url, reference);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ResolveGitLabCommitAsync(
        HttpClient client,
        GitModuleReference reference,
        CancellationToken cancellationToken)
    {
        var projectPath = Uri.EscapeDataString($"{reference.Owner}/{reference.Repository}");
        var url =
            $"{BaseUrl(reference)}/api/v4/projects/{projectPath}/repository/commits/{Uri.EscapeDataString(reference.Ref)}";
        using var request = CreateRequest(HttpMethod.Get, url, reference);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("id", out var idElement))
        {
            throw new InvalidOperationException($"GitLab commit response missing 'id' for '{reference.Label}'.");
        }

        return idElement.GetString()
            ?? throw new InvalidOperationException($"GitLab commit id was null for '{reference.Label}'.");
    }

    private static async Task<byte[]> FetchGitLabArchiveAsync(
        HttpClient client,
        GitModuleReference reference,
        string commitSha,
        CancellationToken cancellationToken)
    {
        var projectPath = Uri.EscapeDataString($"{reference.Owner}/{reference.Repository}");
        var url =
            $"{BaseUrl(reference)}/api/v4/projects/{projectPath}/repository/archive.zip?sha={Uri.EscapeDataString(commitSha)}";
        using var request = CreateRequest(HttpMethod.Get, url, reference);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, GitModuleReference reference)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("statevia-git-modules");
        if (string.IsNullOrEmpty(reference.Token))
        {
            return request;
        }

        switch (reference.Provider)
        {
            case GitModuleProviders.GitHub:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", reference.Token);
                break;
            case GitModuleProviders.GitLab:
                request.Headers.Add("PRIVATE-TOKEN", reference.Token);
                break;
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var snippet = body.Length > 200 ? body[..200] : body;
        throw new HttpRequestException(
            $"Git HTTP request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {snippet}");
    }

    /// <summary>40 桁 hex のフル commit SHA か。</summary>
    internal static bool LooksLikeFullCommitSha(string value) =>
        value.Length == 40 && value.All(static c => Uri.IsHexDigit(c));
}

internal static partial class HttpGitArtifactFetcherLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Resolved Git commit for '{Reference}' ({Sha})")]
    public static partial void CommitResolved(ILogger logger, string reference, string sha);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Fetched Git archive for '{Reference}' (commit {Sha})")]
    public static partial void ArchiveFetched(ILogger logger, string reference, string sha);
}
