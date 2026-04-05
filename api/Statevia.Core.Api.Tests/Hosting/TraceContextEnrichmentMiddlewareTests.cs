using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: ルート後ドメイン ID enrich の単体テスト。</summary>
public sealed class TraceContextEnrichmentMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_LogsEnrichAndSetsTracestate_ForWorkflowRoute()
    {
        // RequestLoggingMiddleware より後段で TraceId が既に Items に入っている状態を模す
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/v1/workflows/wf-abc";
        ctx.Request.RouteValues["id"] = "wf-abc";
        ctx.Items[RequestLogContext.TraceIdItemKey] = "tid-1";
        ctx.Response.Body = new MemoryStream();

        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<TraceContextEnrichmentMiddleware>();
        var opts = Options.Create(new RequestLogOptions
        {
            EmitTracestateWithDomainIds = true,
            LogRequestBody = false,
            LogResponseBody = false
        });

        RequestDelegate next = async c => await c.Response.WriteAsync("ok").ConfigureAwait(false);

        var mw = new TraceContextEnrichmentMiddleware(next);
        await mw.InvokeAsync(ctx, logger, opts).ConfigureAwait(false);

        Assert.Contains(collector.Entries, e => e.Contains("HTTP trace enrich", StringComparison.Ordinal));
        Assert.Contains(collector.Entries, e => e.Contains("wf-abc", StringComparison.Ordinal));
        Assert.True(ctx.Items.ContainsKey(RequestLogContext.WorkflowDisplayIdItemKey));
        Assert.Equal("wf-abc", ctx.Items[RequestLogContext.WorkflowDisplayIdItemKey]);
    }

    [Fact]
    public void ExtractDomainIds_ReturnsDefinitionId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v1/definitions/def-1";
        ctx.Request.RouteValues["id"] = "def-1";
        var (wf, def, g) = TraceContextEnrichmentMiddleware.ExtractDomainIds(ctx);
        Assert.Null(wf);
        Assert.Equal("def-1", def);
        Assert.Null(g);
    }

    [Fact]
    public void BuildTracestateOpaque_Truncates()
    {
        var longId = new string('x', 500);
        var opaque = TraceContextEnrichmentMiddleware.BuildTracestateOpaque(longId, null, null);
        Assert.NotNull(opaque);
        Assert.True(opaque!.Length <= TracestateHelper.MaxOpaqueValueChars);
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
