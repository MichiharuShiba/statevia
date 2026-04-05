using Microsoft.AspNetCore.Http;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: ログ用 trace ID 解決の単体テスト。</summary>
public sealed class TraceIdResolverTests
{
    [Fact]
    public void ResolveTraceId_UsesTraceParentTraceId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["traceparent"] =
            "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        var id = TraceIdResolver.ResolveTraceId(ctx.Request);

        Assert.Equal("0af7651916cd43dd8448eb211c80319c", id);
    }

    [Fact]
    public void ResolveTraceId_InvalidTraceParent_FallsBackToXTraceId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["traceparent"] = "invalid";
        ctx.Request.Headers["X-Trace-Id"] = "  my-trace  ";

        var id = TraceIdResolver.ResolveTraceId(ctx.Request);

        Assert.Equal("my-trace", id);
    }

    [Fact]
    public void ResolveTraceId_XTraceIdTooLong_FallsBackToXRequestId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Trace-Id"] = new string('a', 129);
        ctx.Request.Headers["X-Request-Id"] = "req-1";

        var id = TraceIdResolver.ResolveTraceId(ctx.Request);

        Assert.Equal("req-1", id);
    }

    [Fact]
    public void TryParseTraceParent_Valid_ReturnsTrue()
    {
        var ok = TraceIdResolver.TryParseTraceParent(
            "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            out var tid);

        Assert.True(ok);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", tid);
    }

    [Fact]
    public void TryParseTraceParent_InvalidLength_ReturnsFalse()
    {
        var ok = TraceIdResolver.TryParseTraceParent(
            "00-short-b7ad6b7169203331-01",
            out _);

        Assert.False(ok);
    }
}
