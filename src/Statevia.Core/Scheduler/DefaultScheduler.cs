namespace Statevia.Core.Scheduler;

/// <summary>
/// IScheduler の既定実装。ExecutionLimiter により並列数を制限します。
/// </summary>
public sealed class DefaultScheduler : IScheduler
{
    private readonly ExecutionLimiter _limiter;

    public DefaultScheduler(int maxParallelism = 4) => _limiter = new ExecutionLimiter(maxParallelism);

    /// <inheritdoc />
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
        => _limiter.RunAsync(work, ct);
}
