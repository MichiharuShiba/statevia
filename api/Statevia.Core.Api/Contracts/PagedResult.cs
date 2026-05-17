using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Statevia.Core.Api.Contracts;

/// <summary>一覧 API のページング（O1）。</summary>
/// <typeparam name="T">1 ページ内の要素型。</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>ページ内の要素。</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>条件に一致する総件数。</summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    /// <summary>先頭からのオフセット。</summary>
    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    /// <summary>ページサイズ。</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    /// <summary>次ページが存在するか。</summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }
}
