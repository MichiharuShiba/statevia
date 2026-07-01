namespace Statevia.Core.Engine.Scheduler;

/// <summary>
/// セマフォを用いて同時実行数を制限します。
/// </summary>
public sealed class ExecutionLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    /// <summary>
    /// 指定の最大並列度でセマフォ制限を構築する。
    /// </summary>
    /// <param name="maxParallelism">同時実行の上限（1 以上であること）。</param>
    public ExecutionLimiter(int maxParallelism) => _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

    /// <summary>
    /// スロットを取得してから <paramref name="work"/> を実行し、解放する。
    /// </summary>
    /// <typeparam name="T"><paramref name="work"/> の戻り値の型。</typeparam>
    /// <param name="work">キャンセル可能な非同期処理。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns><paramref name="work"/> の結果。</returns>
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(work);
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try { return await work(ct).ConfigureAwait(false); }
        finally { _semaphore.Release(); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _semaphore.Dispose();
        _disposed = true;
    }
}
