using System.Text;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>rest action の HTTP レスポンスを出力辞書へ変換する。</summary>
internal static class RestHttpResponseMapper
{
    private const int MaxBodyBytes = 1_048_576;

    /// <summary>HTTP レスポンスを action 出力へ変換する。</summary>
    public static async Task<Dictionary<string, object?>> MapAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (Encoding.UTF8.GetByteCount(responseBody) > MaxBodyBytes)
        {
            responseBody = responseBody[..MaxBodyBytes];
        }

        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (object?)string.Join(", ", group.SelectMany(item => item.Value)),
                StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["headers"] = responseHeaders,
            ["body"] = responseBody,
        };
    }
}
