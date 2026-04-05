using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: リクエスト開始/完了ログと X-Trace-Id 方針の単体テスト。</summary>
public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_LogsStartAndComplete()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/v1/health";
        ctx.Response.StatusCode = 200;

        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<RequestLoggingMiddleware>();
        var opts = Options.Create(new RequestLogOptions
        {
            LogRequestBody = false,
            LogResponseBody = false
        });

        RequestDelegate next = c =>
        {
            c.Response.StatusCode = 204;
            return Task.CompletedTask;
        };

        var mw = new RequestLoggingMiddleware(next);
        await mw.InvokeAsync(ctx, logger, opts);

        Assert.Contains(collector.Entries, e => e.Contains("HTTP request start", StringComparison.Ordinal));
        Assert.Contains(collector.Entries, e => e.Contains("HTTP request complete", StringComparison.Ordinal));
        Assert.Contains(collector.Entries, e => e.Contains("204", StringComparison.Ordinal));
        Assert.True(ctx.Items.ContainsKey(RequestLogContext.TraceIdItemKey));
    }

    [Fact]
    public async Task InvokeAsync_SkipsXTraceId_WhenRequestHeaderMatchesResolved()
    {
        // クライアント値と解決 traceId が同一のときは OnStarting を登録しない（重複ヘッダ回避）
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/v1/health";
        ctx.Request.Headers["X-Trace-Id"] = "same-id-please-use-ascii";
        ctx.Response.Body = new MemoryStream();

        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<RequestLoggingMiddleware>();
        var opts = Options.Create(new RequestLogOptions
        {
            LogRequestBody = false,
            LogResponseBody = false,
            EmitXTraceIdResponseHeader = true
        });

        RequestDelegate next = async c => await c.Response.WriteAsync("ok").ConfigureAwait(false);

        var mw = new RequestLoggingMiddleware(next);
        await mw.InvokeAsync(ctx, logger, opts).ConfigureAwait(false);

        Assert.False(ctx.Response.Headers.ContainsKey("X-Trace-Id"));
    }

    [Fact]
    public async Task InvokeAsync_LogsErrorOnUnhandledException()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/x";

        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<RequestLoggingMiddleware>();
        var opts = Options.Create(new RequestLogOptions { LogRequestBody = false, LogResponseBody = false });

        RequestDelegate next = _ => throw new InvalidOperationException("boom");

        var mw = new RequestLoggingMiddleware(next);
        await Assert.ThrowsAsync<InvalidOperationException>(() => mw.InvokeAsync(ctx, logger, opts));

        Assert.Contains(collector.Entries, e => e.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class LogCollector : ILoggerProvider, IDisposable
    {
        public List<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CollLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CollLogger : ILogger
        {
            private readonly List<string> _entries;

            public CollLogger(List<string> entries) => _entries = entries;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _entries.Add(formatter(state, exception));
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
