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

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing) { _semaphore.Dispose(); _disposed = true; }
        GC.SuppressFinalize(this);
    }
}
