using System.Net;
using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Reference.Http;

/// <summary>HTTPS リクエストを送るリファレンス Action。</summary>
internal sealed class HttpRequestActionState : IState<object?, object?>
{
    private const int DefaultTimeoutSeconds = 30;

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveAddressesAsync;

    /// <summary>既定の <see cref="HttpClient"/> とシステム DNS を使う。</summary>
    public HttpRequestActionState()
        : this(static () => new HttpClient(), HttpUrlValidator.ResolveDnsAsync)
    {
    }

    /// <summary>テスト用に HTTP クライアント生成を差し替える。</summary>
    /// <param name="httpClientFactory">リクエストごとにクライアントを返すファクトリ。</param>
    internal HttpRequestActionState(Func<HttpClient> httpClientFactory)
        : this(httpClientFactory, HttpUrlValidator.ResolveDnsAsync)
    {
    }

    /// <summary>テスト用に HTTP クライアントと DNS 解決を差し替える。</summary>
    /// <param name="httpClientFactory">リクエストごとにクライアントを返すファクトリ。</param>
    /// <param name="resolveAddressesAsync">ホスト名の A/AAAA 解決。</param>
    internal HttpRequestActionState(
        Func<HttpClient> httpClientFactory,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync)
    {
        _httpClientFactory = httpClientFactory;
        _resolveAddressesAsync = resolveAddressesAsync;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        _ = ctx;
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("http.request action requires input.url and input.method.");
        }

        var url = ActionInputReader.RequireString(fields, "url");
        var method = ActionInputReader.RequireString(fields, "method");
        await HttpUrlValidator.EnsureAllowedHttpsUrlAsync(url, _resolveAddressesAsync, ct).ConfigureAwait(false);

        using var httpClient = _httpClientFactory();
        httpClient.Timeout = TimeSpan.FromSeconds(ResolveTimeoutSeconds(fields));
        using var request = HttpRequestBuilder.Build(fields, url, method);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return await HttpResponseMapper.MapAsync(response, ct).ConfigureAwait(false);
    }

    private static int ResolveTimeoutSeconds(IReadOnlyDictionary<string, JsonElement> fields) =>
        fields.TryGetValue("timeout", out var timeoutElement)
            && timeoutElement.TryGetInt32(out var timeoutValue)
            ? timeoutValue
            : DefaultTimeoutSeconds;
}
