using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// GET /v1/executions/{id}/state のクエリパラメータ。
/// </summary>
public sealed class ExecutionStateQuery
{
    /// <summary>復元対象の event_store シーケンス（1 以上）。</summary>
    [FromQuery(Name = "atSeq")]
    [Range(1, long.MaxValue, ErrorMessage = "atSeq must be >= 1")]
    public long AtSeq { get; init; }
}
