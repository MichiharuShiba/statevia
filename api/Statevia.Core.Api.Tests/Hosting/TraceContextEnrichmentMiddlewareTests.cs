using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: ルート後ドメイン ID enrich の単体テスト。</summary>
public sealed class TraceContextEnrichmentMiddlewareTests
{
    /// <summary>TraceId が Items に無いときは enrich ログを出さない。</summary>
    [Fact]
    public async Task InvokeAsync_SkipsEnrichLog_WhenTraceIdMissing()
    {
        // Arrange
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/v1/executions/wf-abc";
        ctx.Request.RouteValues["id"] = "wf-abc";
        ctx.Response.Body = new MemoryStream();

        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<TraceContextEnrichmentMiddleware>();
        var opts = Options.Create(new RequestLogOptions { EmitTracestateWithDomainIds = true });
        var middleware = new TraceContextEnrichmentMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(ctx, logger, opts);

        // Assert
        Assert.DoesNotContain(collector.Entries, e => e.Contains("HTTP trace enrich", StringComparison.Ordinal));
    }

    /// <summary>
    /// 実行ルートで Items へのドメイン ID 設定と enrich ログ出力を行う。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LogsEnrichAndSetsTracestate_ForExecutionRoute()
    {
        // Arrange
        // RequestLoggingMiddleware より後段で TraceId が既に Items に入っている状態を模す。
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/v1/executions/wf-abc";
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
        RequestDelegate next = c => c.Response.WriteAsync("ok");
        var middleware = new TraceContextEnrichmentMiddleware(next);

        // Act
        await middleware.InvokeAsync(ctx, logger, opts);

        // Assert
        Assert.Contains(collector.Entries, e => e.Contains("HTTP trace enrich", StringComparison.Ordinal));
        Assert.Contains(collector.Entries, e => e.Contains("wf-abc", StringComparison.Ordinal));
        Assert.True(ctx.Items.ContainsKey(RequestLogContext.ExecutionDisplayIdItemKey));
        Assert.Equal("wf-abc", ctx.Items[RequestLogContext.ExecutionDisplayIdItemKey]);
    }

    /// <summary>
    /// <see cref="TraceContextEnrichmentMiddleware.ExtractDomainIds"/> が定義ルートから definition ID を返す。
    /// </summary>
    [Fact]
    public void ExtractDomainIds_ReturnsDefinitionId()
    {
        // Arrange
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v1/definitions/def-1";
        ctx.Request.RouteValues["id"] = "def-1";

        // Act
        var (executionId, definitionId, graphId) = TraceContextEnrichmentMiddleware.ExtractDomainIds(ctx);

        // Assert
        Assert.Null(executionId);
        Assert.Equal("def-1", definitionId);
        Assert.Null(graphId);
    }

    /// <summary>
    /// <see cref="TraceContextEnrichmentMiddleware.ExtractDomainIds"/> がグラフルートから graph ID を返す。
    /// </summary>
    [Fact]
    public void ExtractDomainIds_ReturnsGraphId()
    {
        // Arrange
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v1/graphs/graph-42";
        ctx.Request.RouteValues["graphId"] = "graph-42";

        // Act
        var (executionId, definitionId, graphId) = TraceContextEnrichmentMiddleware.ExtractDomainIds(ctx);

        // Assert
        Assert.Null(executionId);
        Assert.Null(definitionId);
        Assert.Equal("graph-42", graphId);
    }

    /// <summary>
    /// <see cref="TraceContextEnrichmentMiddleware.BuildTracestateOpaque"/> が execution ID を不透明値に含める。
    /// </summary>
    [Fact]
    public void BuildTracestateOpaque_IncludesExecutionId()
    {
        // Arrange
        const string executionDisplayId = "wf-1";

        // Act
        var opaque = TraceContextEnrichmentMiddleware.BuildTracestateOpaque(executionDisplayId, null, null);

        // Assert
        Assert.NotNull(opaque);
        Assert.Contains("w=wf-1", opaque, StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="TraceContextEnrichmentMiddleware.BuildTracestateOpaque"/> が上限長を超える値を切り詰める。
    /// </summary>
    [Fact]
    public void BuildTracestateOpaque_Truncates()
    {
        // Arrange
        var longExecutionId = new string('x', 500);

        // Act
        var opaque = TraceContextEnrichmentMiddleware.BuildTracestateOpaque(longExecutionId, null, null);

        // Assert
        Assert.NotNull(opaque);
        Assert.True(opaque!.Length <= TracestateHelper.MaxOpaqueValueChars);
    }

    /// <summary>グラフルートで graph ID を Items と enrich ログへ載せる。</summary>
    [Fact]
    public async Task InvokeAsync_LogsEnrich_ForGraphRoute()
    {
        // Arrange
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/v1/graphs/graph-42";
        ctx.Request.RouteValues["graphId"] = "graph-42";
        ctx.Items[RequestLogContext.TraceIdItemKey] = "tid-graph";
        ctx.Response.Body = new MemoryStream();

        var collector = new LogCollector();
        using var factory = LoggerFactory.Create(b => b.AddProvider(collector));
        var logger = factory.CreateLogger<TraceContextEnrichmentMiddleware>();
        var opts = Options.Create(new RequestLogOptions { EmitTracestateWithDomainIds = true });
        var middleware = new TraceContextEnrichmentMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(ctx, logger, opts);

        // Assert
        Assert.Contains(collector.Entries, e => e.Contains("HTTP trace enrich", StringComparison.Ordinal));
        Assert.Contains(collector.Entries, e => e.Contains("graph-42", StringComparison.Ordinal));
        Assert.Equal("graph-42", ctx.Items[RequestLogContext.GraphDefinitionIdItemKey]);
    }

    /// <summary>構造化ログ出力を捕捉するテスト用 <see cref="ILoggerProvider"/>。</summary>
    private sealed class LogCollector : ILoggerProvider, IDisposable
    {
        /// <summary>捕捉したログメッセージ一覧。</summary>
        public List<string> Entries { get; } = [];

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new CollLogger(Entries);

        /// <inheritdoc />
        public void Dispose()
        {
        }

        private sealed class CollLogger : ILogger
        {
            private readonly List<string> _entries;

            /// <summary>ログを書き込む先のリストを指定する。</summary>
            public CollLogger(List<string> entries) => _entries = entries;

            /// <inheritdoc />
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

            /// <inheritdoc />
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <inheritdoc />
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
            /// <summary>共有の空スコープ。</summary>
            public static readonly NoopScope Instance = new();

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
