using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>rest action の HTTP リクエスト組み立て。</summary>
internal static class RestHttpRequestBuilder
{
    private const int MaxBodyBytes = 1_048_576;

    /// <summary>入力フィールドから <see cref="HttpRequestMessage"/> を構築する。</summary>
    public static HttpRequestMessage Build(
        IReadOnlyDictionary<string, JsonElement> fields,
        string url,
        string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        ApplyHeaders(request, fields);
        ApplyIdempotencyKey(request, fields);
        ApplyBody(request, fields);
        return request;
    }

    private static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, JsonElement> fields)
    {
        if (!fields.TryGetValue("headers", out var headersElement)
            || headersElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var header in headersElement.EnumerateObject())
        {
            if (header.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString());
        }
    }

    private static void ApplyIdempotencyKey(HttpRequestMessage request, IReadOnlyDictionary<string, JsonElement> fields)
    {
        var idempotencyKey = ActionInputReader.OptionalString(fields, "idempotencyKey");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
    }

    private static void ApplyBody(HttpRequestMessage request, IReadOnlyDictionary<string, JsonElement> fields)
    {
        if (!fields.TryGetValue("body", out var bodyElement)
            || bodyElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array))
        {
            return;
        }

        var bodyText = bodyElement.ValueKind == JsonValueKind.String
            ? bodyElement.GetString() ?? string.Empty
            : bodyElement.GetRawText();
        if (Encoding.UTF8.GetByteCount(bodyText) > MaxBodyBytes)
        {
            throw new ArgumentException("rest action body exceeds the maximum allowed size.");
        }

        request.Content = new StringContent(bodyText, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }
}
