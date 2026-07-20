using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/executions/{id}/events のクエリパラメータ。
/// </summary>
public sealed class ExecutionEventsQuery
{
    /// <summary>このシーケンスより後のイベントを返す（0 以上。省略時 0）。</summary>
    [FromQuery(Name = "afterSeq")]
    [Range(0, long.MaxValue, ErrorMessage = "afterSeq must be >= 0")]
    public long AfterSeq { get; init; }

    /// <summary>取得件数（1〜5000。省略時 500）。</summary>
    [FromQuery(Name = "limit")]
    [Range(1, 5000, ErrorMessage = "limit must be between 1 and 5000")]
    public int Limit { get; init; } = 500;
}
