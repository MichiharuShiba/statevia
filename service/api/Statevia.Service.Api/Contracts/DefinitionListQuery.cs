using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/definitions のクエリパラメータ（ページング・フィルタ・ソート）。
/// </summary>
public sealed class DefinitionListQuery
{
    /// <summary>ページサイズ（必須。1〜500）。</summary>
    [FromQuery(Name = "limit")]
    [Required(ErrorMessage = "limit is required")]
    [Range(1, 500, ErrorMessage = "limit must be at most 500")]
    public int? Limit { get; init; }

    /// <summary>オフセット（0 以上）。</summary>
    [FromQuery(Name = "offset")]
    [Range(0, int.MaxValue, ErrorMessage = "offset must be >= 0")]
    public int? Offset { get; init; }

    /// <summary>定義名の部分一致。</summary>
    [FromQuery(Name = "name")]
    public string? Name { get; init; }

    /// <summary>ソート列。</summary>
    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }

    /// <summary>ソート順。</summary>
    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; init; }

    /// <summary>削除済み定義を一覧に含める（省略時は false）。</summary>
    [FromQuery(Name = "includeDeleted")]
    public bool? IncludeDeleted { get; init; }
}
