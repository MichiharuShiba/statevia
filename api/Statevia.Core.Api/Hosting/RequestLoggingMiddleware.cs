using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// リクエスト開始・完了・未処理例外を <see cref="ILogger"/> に記録する。
/// </summary>
internal sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<RequestLogOptions> optionsAccessor)
    {
        var opts = optionsAccessor.Value;
        var traceId = TraceIdResolver.ResolveTraceId(context.Request);
        // 後続ミドルウェア・フィルタと共有（enrich ログ等）
        context.Items[RequestLogContext.TraceIdItemKey] = traceId;

        // 応答送信直前に付与（書き込み済みヘッダと競合しないよう OnStarting）
        if (opts.EmitXTraceIdResponseHeader && !ShouldSkipEmitXTraceId(context.Request, traceId))
        {
            context.Response.OnStarting(
                static state =>
                {
                    var (ctx, tid) = ((HttpContext, string))state!;
                    ctx.Response.Headers["X-Trace-Id"] = tid;
                    return Task.CompletedTask;
                },
                (context, traceId));
        }

        var tenantId = context.Request.Headers[TenantHeader.HeaderName].FirstOrDefault()
                       ?? TenantHeader.DefaultTenantId;
        var path = (context.Request.PathBase + context.Request.Path).Value ?? "";
        var queryForLog = BuildQueryForLog(context.Request.QueryString, opts);

        string? requestBodyLog = null;
        try
        {
            requestBodyLog = await BuildRequestBodyLogAsync(context.Request, opts).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            requestBodyLog = "[request body read error]";
        }
        catch (NotSupportedException)
        {
            requestBodyLog = "[request body read error]";
        }
        catch (InvalidOperationException)
        {
            requestBodyLog = "[request body read error]";
        }

        var userAgent = TruncateUserAgent(context.Request.Headers.UserAgent.ToString());

        // Path と Query は分離（要件: 一覧検索・相関しやすくする）
        TryLog(() =>
            logger.LogInformation(
                "HTTP request start TraceId={TraceId} Method={Method} Path={Path} Query={Query} TenantId={TenantId} UserAgent={UserAgent} RequestBody={RequestBody}",
                traceId,
                context.Request.Method,
                path,
                queryForLog,
                tenantId,
                string.IsNullOrEmpty(userAgent) ? null : userAgent,
                requestBodyLog));

        var sw = Stopwatch.StartNew();
        Stream? originalBody = null;
        ResponseBodyLoggingStream? captureStream = null;
        // オプション時のみ Body をラップ（完了ログで先頭スナップショットを取る）
        if (opts.LogResponseBody)
        {
            originalBody = context.Response.Body;
            captureStream = new ResponseBodyLoggingStream(originalBody, opts.MaxResponseBodyLogBytes);
            context.Response.Body = captureStream;
        }

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryLog(() =>
                logger.LogError(ex,
                    "HTTP request unhandled exception TraceId={TraceId} ExceptionType={ExceptionType} Message={Message}",
                    traceId,
                    ex.GetType().FullName,
                    ex.Message));
            throw;
        }
        finally
        {
            sw.Stop();
            long? responseSize = null;
            string responseBodyLog = "";

            // ラップを外して実ストリームへ戻し、保持バイトからログ用文字列を組み立てる
            if (captureStream != null && originalBody != null)
            {
                context.Response.Body = originalBody;
                responseSize = captureStream.TotalBytesWritten;
                var bytes = captureStream.GetCapturedBytes();
                try
                {
                    responseBodyLog = BuildResponseBodyLog(bytes, context.Response.ContentType, opts);
                }
#pragma warning disable CA1031 // 応答ログ用デコード／マスキング失敗でもリクエスト完了ログは続行する
                catch (Exception)
                {
                    responseBodyLog = "[response body decode error]";
                }
#pragma warning restore CA1031

                captureStream.Dispose();
            }
            // 本文キャプチャ無しでも Content-Length があればサイズだけ記録
            else if (context.Response.ContentLength is { } clen)
            {
                responseSize = clen;
            }

            var status = context.Response.StatusCode;
            TryLog(() =>
                logger.LogInformation(
                    "HTTP request complete TraceId={TraceId} StatusCode={StatusCode} ElapsedMs={ElapsedMs} ResponseSize={ResponseSize} ResponseBody={ResponseBody}",
                    traceId,
                    status,
                    sw.ElapsedMilliseconds,
                    responseSize,
                    string.IsNullOrEmpty(responseBodyLog) ? null : responseBodyLog));
        }
    }

    private static string BuildQueryForLog(QueryString queryString, RequestLogOptions opts)
    {
        var raw = queryString.Value;
        if (string.IsNullOrEmpty(raw))
            return "";
        // 長さ制限後にクエリパラメータ名のマスク（?password= 等）
        var q = raw.Length <= opts.MaxQueryStringChars
            ? raw
            : raw[..opts.MaxQueryStringChars] + "...[truncated]";
        return LogBodyRedactor.Redact(q, q.Length);
    }

    private static async Task<string?> BuildRequestBodyLogAsync(HttpRequest request, RequestLogOptions opts)
    {
        if (!opts.LogRequestBody)
            return null;

        if (!IsTextualContentType(request.ContentType))
            return "[non-text body omitted]";

        var len = request.ContentLength;
        if (len.HasValue && len.Value > opts.MaxRequestBodyLogBytes)
            return $"[omitted: request body larger than {opts.MaxRequestBodyLogBytes} bytes]";

        if (IsChunkedWithoutLength(request) && len == null)
            return "[chunked body omitted]";

        // 下流（モデルバインディング）が再度読めるようバッファ化
        request.EnableBuffering();
        var buffer = new byte[opts.MaxRequestBodyLogBytes];
        int read;
        try
        {
            read = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                request.Body.Position = 0;
            }
            catch (IOException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (read == 0)
            return null;

        var text = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
        return LogBodyRedactor.Redact(text, text.Length);
    }

    private static bool IsChunkedWithoutLength(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Transfer-Encoding", out var te))
            return false;
        return te.ToString().Contains("chunked", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildResponseBodyLog(ReadOnlyMemory<byte> captured, string? contentType, RequestLogOptions opts)
    {
        if (!opts.LogResponseBody || captured.Length == 0)
            return "";

        if (!IsTextualContentType(contentType))
            return "[non-text response omitted]";

        var text = Encoding.UTF8.GetString(captured.Span);
        return LogBodyRedactor.Redact(text, text.Length);
    }

    private static bool IsTextualContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return true; // 未指定はテキスト扱いで試す
        var semi = contentType.IndexOf(';');
        var media = semi >= 0 ? contentType[..semi] : contentType;
        media = media.Trim();
        return media.Equals("application/json", StringComparison.OrdinalIgnoreCase)
               || media.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
               || media.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TruncateUserAgent(string? ua)
    {
        if (string.IsNullOrEmpty(ua))
            return null;
        const int max = 256;
        return ua.Length <= max ? ua : ua[..max] + "...";
    }

    private static void TryLog(Action logAction)
    {
#pragma warning disable CA1031 // 構造化ログ提供側の異常でもパイプラインへ影響を与えない
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

    /// <summary>
    /// クライアントが送った <c>X-Trace-Id</c> が解決結果と同一なら、応答への重複付与を避ける。
    /// </summary>
    private static bool ShouldSkipEmitXTraceId(HttpRequest request, string traceId)
    {
        var raw = request.Headers["X-Trace-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var t = raw.Trim();
        if (t.Length == 0 || t.Length > 128)
            return false;
        return string.Equals(t, traceId, StringComparison.Ordinal);
    }
}
