using System.Collections.Generic;

namespace Statevia.Core.Api.Contracts;

/// <summary>一覧 API のページング（O1）。</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
    public bool HasMore { get; init; }
}
