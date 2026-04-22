using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Statevia.Core.Api.Contracts;

/// <summary>一覧 API のページング（O1）。</summary>
public sealed class PagedResult<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }
}
