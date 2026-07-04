using Microsoft.AspNetCore.Http;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Infrastructure;

/// <summary>
/// <see cref="ICorrelationIdAccessor"/> の HTTP 実装。<see cref="RequestLogContext.TraceIdItemKey"/> から相関 ID を返す。
/// </summary>
internal sealed class HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public string GetCorrelationId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(RequestLogContext.TraceIdItemKey, out var traceObject) is true
            && traceObject is string traceId)
            return traceId;

        return string.Empty;
    }
}
