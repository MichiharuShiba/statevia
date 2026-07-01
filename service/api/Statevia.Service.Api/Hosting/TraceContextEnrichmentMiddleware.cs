using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// ルート確定後にドメイン ID を <see cref="HttpContext.Items"/> とログへ載せ、任意で応答 <c>tracestate</c> を更新する。
/// </summary>
internal sealed class TraceContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public TraceContextEnrichmentMiddleware(RequestDelegate next) => _next = next;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Critical Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "トレース enrich と tracestate 付与を単一ミドルウェアに集約している。")]
    public async Task InvokeAsync(
        HttpContext context,
        ILogger<TraceContextEnrichmentMiddleware> logger,
        IOptions<RequestLogOptions> optionsAccessor)
    {
        var opts = optionsAccessor.Value;
        // UseRouting より後でのみ RouteValues が信頼できる
        var (wf, def, graph) = ExtractDomainIds(context);

        if (context.Items.TryGetValue(RequestLogContext.TraceIdItemKey, out var tidObj) && tidObj is string traceId)
        {
            if (!string.IsNullOrEmpty(wf))
                context.Items[RequestLogContext.ExecutionDisplayIdItemKey] = wf;
            if (!string.IsNullOrEmpty(def))
                context.Items[RequestLogContext.DefinitionDisplayIdItemKey] = def;
            if (!string.IsNullOrEmpty(graph))
                context.Items[RequestLogContext.GraphDefinitionIdItemKey] = graph;

            // いずれかのドメイン ID が取れたときだけ 1 行（空フィールドは構造化ログで null）
            if (!string.IsNullOrEmpty(wf) || !string.IsNullOrEmpty(def) || !string.IsNullOrEmpty(graph))
            {
                TryLog(() =>
                    logger.HttpTraceEnrich(
                        traceId,
                        string.IsNullOrEmpty(wf) ? null : wf,
                        string.IsNullOrEmpty(def) ? null : def,
                        string.IsNullOrEmpty(graph) ? null : graph));
            }
        }

        if (opts.EmitTracestateWithDomainIds)
        {
            var opaque = BuildTracestateOpaque(wf, def, graph);
            if (!string.IsNullOrEmpty(opaque))
            {
                var existing = context.Request.Headers.TraceState.FirstOrDefault();
                var merged = TracestateHelper.Merge(existing, TracestateHelper.StateviaVendorKey, opaque);
                // クロージャで merged を固定（応答開始時に一括設定）
                var mergedLocal = merged;
                context.Response.OnStarting(
                    static state =>
                    {
                        var (ctx, m) = ((HttpContext, string))state!;
                        ctx.Response.Headers.TraceState = m;
                        return Task.CompletedTask;
                    },
                    (context, mergedLocal));
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    internal static (string? wf, string? def, string? graph) ExtractDomainIds(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (TryGetRouteString(ctx, "id") is { } routeId)
        {
            if (path.StartsWith("/v1/executions/", StringComparison.OrdinalIgnoreCase))
                return (routeId, null, null);
            if (path.StartsWith("/v1/definitions/", StringComparison.OrdinalIgnoreCase))
                return (null, routeId, null);
        }

        if (path.StartsWith("/v1/graphs/", StringComparison.OrdinalIgnoreCase)
            && TryGetRouteString(ctx, "graphId") is { } graphId)
            return (null, null, graphId);

        return (null, null, null);
    }

    private static string? TryGetRouteString(HttpContext ctx, string routeKey) =>
        ctx.Request.RouteValues.TryGetValue(routeKey, out var value) && value is not null
            ? value.ToString()
            : null;

    internal static string? BuildTracestateOpaque(string? wf, string? def, string? graph)
    {
        if (string.IsNullOrEmpty(wf) && string.IsNullOrEmpty(def) && string.IsNullOrEmpty(graph))
            return null;

        // 短い不透明値（キーは w/d/g、値はパーセントエンコード）
        var sb = new StringBuilder();
        void AppendPair(char key, string? v)
        {
            if (string.IsNullOrEmpty(v))
                return;
            if (sb.Length > 0)
                sb.Append('|');
            sb.Append(key).Append('=').Append(Uri.EscapeDataString(v));
        }

        AppendPair('w', wf);
        AppendPair('d', def);
        AppendPair('g', graph);

        var s = sb.ToString();
        if (s.Length > TracestateHelper.MaxOpaqueValueChars)
            s = s[..TracestateHelper.MaxOpaqueValueChars];
        return s;
    }

    private static void TryLog(Action logAction)
    {
#pragma warning disable CA1031 // 構造化ログ提供側の異常でもミドルウェアを中断しない
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // ログ失敗でリクエストを壊さない
        }
#pragma warning restore CA1031
    }
}
