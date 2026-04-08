using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// ルート確定後にドメイン ID を <see cref="HttpContext.Items"/> とログへ載せ、任意で応答 <c>tracestate</c> を更新する。
/// </summary>
public sealed class TraceContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public TraceContextEnrichmentMiddleware(RequestDelegate next) => _next = next;

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
                context.Items[RequestLogContext.WorkflowDisplayIdItemKey] = wf;
            if (!string.IsNullOrEmpty(def))
                context.Items[RequestLogContext.DefinitionDisplayIdItemKey] = def;
            if (!string.IsNullOrEmpty(graph))
                context.Items[RequestLogContext.GraphDefinitionIdItemKey] = graph;

            // いずれかのドメイン ID が取れたときだけ 1 行（空フィールドは構造化ログで null）
            if (!string.IsNullOrEmpty(wf) || !string.IsNullOrEmpty(def) || !string.IsNullOrEmpty(graph))
            {
                TryLog(() =>
                    logger.LogInformation(
                        "HTTP trace enrich TraceId={TraceId} WorkflowId={WorkflowId} DefinitionId={DefinitionId} GraphDefinitionId={GraphDefinitionId}",
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
                var existing = context.Request.Headers["tracestate"].FirstOrDefault();
                var merged = TracestateHelper.Merge(existing, TracestateHelper.StateviaVendorKey, opaque);
                // クロージャで merged を固定（応答開始時に一括設定）
                var mergedLocal = merged;
                context.Response.OnStarting(
                    static state =>
                    {
                        var (ctx, m) = ((HttpContext, string))state!;
                        ctx.Response.Headers["tracestate"] = m;
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
        var rv = ctx.Request.RouteValues;

        // コントローラの Route プレフィックスとテンプレート引数名に合わせる
        if (path.StartsWith("/v1/workflows/", StringComparison.OrdinalIgnoreCase))
        {
            if (rv.TryGetValue("id", out var id) && id != null)
            {
                var s = id.ToString();
                if (!string.IsNullOrEmpty(s))
                    return (s, null, null);
            }
        }

        if (path.StartsWith("/v1/definitions/", StringComparison.OrdinalIgnoreCase))
        {
            if (rv.TryGetValue("id", out var id) && id != null)
            {
                var s = id.ToString();
                if (!string.IsNullOrEmpty(s))
                    return (null, s, null);
            }
        }

        if (path.StartsWith("/v1/graphs/", StringComparison.OrdinalIgnoreCase))
        {
            if (rv.TryGetValue("graphId", out var gid) && gid != null)
            {
                var s = gid.ToString();
                if (!string.IsNullOrEmpty(s))
                    return (null, null, s);
            }
        }

        return (null, null, null);
    }

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
        try
        {
            logAction();
        }
        catch
        {
            // ログ失敗でリクエストを壊さない
        }
    }
}
