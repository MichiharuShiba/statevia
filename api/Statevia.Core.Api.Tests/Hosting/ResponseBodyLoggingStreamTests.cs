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

    /// <summary>同期 Write でも先頭バイトをキャプチャする。</summary>
    [Fact]
    public void Write_Sync_CapturesPrefix()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var wrap = new ResponseBodyLoggingStream(inner, maxCaptureBytes: 4);
        var data = Encoding.UTF8.GetBytes("abcdEF");

        // Act
        wrap.Write(data, 0, data.Length);

        // Assert
        Assert.Equal("abcd", Encoding.UTF8.GetString(wrap.GetCapturedBytes().Span));
        Assert.Equal(6, wrap.TotalBytesWritten);
    }

    /// <summary>キャプチャ上限到達後は追加バイトを保持しない。</summary>
    [Fact]
    public void Write_StopsCapturing_AfterMaxBytes()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var wrap = new ResponseBodyLoggingStream(inner, maxCaptureBytes: 3);

        // Act
        wrap.Write(Encoding.UTF8.GetBytes("abc"));
        wrap.Write(Encoding.UTF8.GetBytes("def"));

        // Assert
        Assert.Equal(3, wrap.GetCapturedBytes().Length);
        Assert.Equal("abcdef", Encoding.UTF8.GetString(inner.ToArray()));
    }

    /// <summary>読み取りはサポートしない。</summary>
    [Fact]
    public void Read_ThrowsNotSupported()
    {
        // Arrange
        using var wrap = new ResponseBodyLoggingStream(Stream.Null, maxCaptureBytes: 1);
        var buffer = new byte[1];

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => wrap.Read(buffer, 0, 1));
    }
}
