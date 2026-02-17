namespace Statevia.Core.Scheduler;

/// <summary>
/// セマフォを用いて同時実行数を制限します。
/// </summary>
public sealed class ExecutionLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public ExecutionLimiter(int maxParallelism) => _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(work);
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try { return await work(ct).ConfigureAwait(false); }
        finally { _semaphore.Release(); }
    }

    public void Dispose() => Dispose(true);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarLint", "S3971", Justification = "GC.SuppressFinalize is part of the standard IDisposable pattern")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.CodeAnalysis", "CA1816", Justification = "GC.SuppressFinalize is part of the standard IDisposable pattern")]
    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing) { _semaphore.Dispose(); _disposed = true; }
        GC.SuppressFinalize(this);
    }
}
