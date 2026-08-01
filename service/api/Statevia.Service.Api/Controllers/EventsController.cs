using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Controllers;

/// <summary>トピック／キーにより durable Wait 購読へイベントを配送する API。</summary>
[ApiController]
[Route("v1/events")]
public sealed class EventsController(IEventIngressService eventIngress) : ControllerBase
{
    /// <summary>
    /// 現在テナントの配送条件に一致する購読を Resume ワークとして投入する。
    /// </summary>
    /// <param name="request">イベント配送条件（topic 必須、key 任意）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>受理済みを表す 204。対象が 0 件でも同じ応答を返す。</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Publish([FromBody] EventIngressRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await eventIngress.PublishAsync(
            request.Topic,
            request.Key ?? string.Empty,
            ct).ConfigureAwait(false);

        return NoContent();
    }
}
