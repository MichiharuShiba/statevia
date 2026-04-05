using System.Text;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary>STV-403: レスポンス本文キャプチャ用ストリームの単体テスト。</summary>
public sealed class ResponseBodyLoggingStreamTests
{
    [Fact]
    public async Task WriteAsync_ForwardsAndCapturesPrefix()
    {
        await using var inner = new MemoryStream();
        await using var wrap = new ResponseBodyLoggingStream(inner, maxCaptureBytes: 10);
        var data = Encoding.UTF8.GetBytes("hello world wide");
        await wrap.WriteAsync(data);

        Assert.Equal(16, wrap.TotalBytesWritten);
        var cap = wrap.GetCapturedBytes();
        Assert.Equal(10, cap.Length);
        Assert.Equal("hello worl", Encoding.UTF8.GetString(cap.Span));
        Assert.Equal("hello world wide", Encoding.UTF8.GetString(inner.ToArray()));
    }
}
