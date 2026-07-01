using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>HTTPS REST 呼び出しを行う Http capability。</summary>
internal sealed class RestActionState : IState<object?, object?>
{
    private const int DefaultTimeoutSeconds = 30;

    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>スコープ付き HTTP クライアント解決用に構築する。</summary>
    /// <param name="scopeFactory">実行時スコープ生成。</param>
    public RestActionState(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("rest action requires input.url and input.method.");
        }

        var url = ActionInputReader.RequireString(fields, "url");
        var method = ActionInputReader.RequireString(fields, "method");
        RestUrlValidator.EnsureAllowedHttpsUrl(url);

        using var scope = _scopeFactory.CreateScope();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(nameof(RestActionState));
        httpClient.Timeout = TimeSpan.FromSeconds(ResolveTimeoutSeconds(fields));

        using var request = RestHttpRequestBuilder.Build(fields, url, method);
        using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
        return await RestHttpResponseMapper.MapAsync(response, ct).ConfigureAwait(false);
    }

    private static int ResolveTimeoutSeconds(IReadOnlyDictionary<string, System.Text.Json.JsonElement> fields) =>
        fields.TryGetValue("timeout", out var timeoutElement)
            && timeoutElement.TryGetInt32(out var timeoutValue)
            ? timeoutValue
            : DefaultTimeoutSeconds;
}
