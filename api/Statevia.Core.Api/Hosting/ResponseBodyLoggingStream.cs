namespace Statevia.Core.Api.Hosting;

/// <summary>
/// レスポンス本文の先頭 N バイトを保持しつつ下流ストリームへ転送する。
/// </summary>
internal sealed class ResponseBodyLoggingStream : Stream
{
    private readonly Stream _inner;
    private readonly int _maxCaptureBytes;
    private readonly MemoryStream _capture = new();
    private long _totalBytesWritten;

    /// <param name="inner">実レスポンスの書き込み先（Kestrel 等の下流）。</param>
    /// <param name="maxCaptureBytes">ログ用に保持する先頭バイト上限。</param>
    public ResponseBodyLoggingStream(Stream inner, int maxCaptureBytes)
    {
        _inner = inner;
        _maxCaptureBytes = maxCaptureBytes;
    }

    public long TotalBytesWritten => _totalBytesWritten;

    public ReadOnlyMemory<byte> GetCapturedBytes()
    {
        return _capture.ToArray();
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        Capture(buffer.AsSpan(offset, count));
        _inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Capture(buffer);
        _inner.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Capture(buffer.AsSpan(offset, count));
        await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Capture(buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private void Capture(ReadOnlySpan<byte> buffer)
    {
        _totalBytesWritten += buffer.Length;
        // 上限到達後は転送のみ（メモリとログサイズを抑える）
        if (_capture.Length >= _maxCaptureBytes)
            return;
        var room = _maxCaptureBytes - (int)_capture.Length;
        var take = Math.Min(room, buffer.Length);
        _capture.Write(buffer[..take]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _capture.Dispose();
        base.Dispose(disposing);
    }
}
