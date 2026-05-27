using Microsoft.AspNetCore.Mvc;

namespace Statevia.Core.Api.Contracts;

/// <summary>
/// GET /v1/executions のクエリパラメータ（ページング・フィルタ・ソート）。
/// </summary>
public sealed class ExecutionListQuery
{
    /// <summary>ページサイズ（必須。1〜500）。</summary>
    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    /// <summary>オフセット（0 以上）。</summary>
    [FromQuery(Name = "offset")]
    public int? Offset { get; init; }

    /// <summary>ステータスフィルタ。</summary>
    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    /// <summary>定義 ID（display または UUID）。</summary>
    [FromQuery(Name = "definitionId")]
    public string? DefinitionId { get; init; }

    /// <summary>表示 ID 部分一致または execution UUID 完全一致。</summary>
    [FromQuery(Name = "name")]
    public string? Name { get; init; }

    /// <summary>ソート列。</summary>
    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }

    /// <summary>ソート順。</summary>
    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; init; }
}
