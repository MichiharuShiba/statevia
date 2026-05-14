namespace Statevia.Core.Engine.Scheduler;

/// <summary>
/// IScheduler の既定実装。ExecutionLimiter により並列数を制限します。
/// </summary>
public sealed class DefaultScheduler : IScheduler, IDisposable
{
    private readonly ExecutionLimiter _limiter;
    private bool _disposed;

    /// <summary>
    /// 既定の並列度でスケジューラを構築する。
    /// </summary>
    /// <param name="maxParallelism">同時に実行できる状態タスクの上限。</param>
    public DefaultScheduler(int maxParallelism = 4) => _limiter = new ExecutionLimiter(maxParallelism);

    /// <inheritdoc />
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _limiter.RunAsync(work, ct);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _limiter.Dispose();
        _disposed = true;
    }
}
