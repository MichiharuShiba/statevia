namespace Statevia.Core.Scheduler;

/// <summary>
/// セマフォを用いて同時実行数を制限します。
/// </summary>
public sealed class ExecutionLimiter
{
    private readonly SemaphoreSlim _semaphore;

    public ExecutionLimiter(int maxParallelism) => _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try { return await work(ct); }
        finally { _semaphore.Release(); }
    }
}
