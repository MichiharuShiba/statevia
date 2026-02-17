namespace Statevia.Core.Scheduler;

/// <summary>
/// IScheduler の既定実装。ExecutionLimiter により並列数を制限します。
/// </summary>
public sealed class DefaultScheduler : IScheduler, IDisposable
{
    private readonly ExecutionLimiter _limiter;
    private bool _disposed;

    public DefaultScheduler(int maxParallelism = 4) => _limiter = new ExecutionLimiter(maxParallelism);

    /// <inheritdoc />
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _limiter.RunAsync(work, ct);
    }

    /// <inheritdoc />
    public void Dispose() => Dispose(true);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarLint", "S3971", Justification = "GC.SuppressFinalize is part of the standard IDisposable pattern")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.CodeAnalysis", "CA1816", Justification = "GC.SuppressFinalize is part of the standard IDisposable pattern")]
    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing) { _limiter.Dispose(); _disposed = true; }
        GC.SuppressFinalize(this);
    }
}
