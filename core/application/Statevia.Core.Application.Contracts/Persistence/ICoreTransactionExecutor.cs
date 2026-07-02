namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// ReadCommitted / ReadOnly の定型トランザクション実行。
/// </summary>
public interface ICoreTransactionExecutor
{
    /// <summary>ReadCommitted で 1 トランザクションを実行し、成功時にコミットする。</summary>
    Task ExecuteReadCommittedAsync(
        Func<ICoreUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>ReadCommitted で 1 トランザクションを実行し、結果を返す。</summary>
    Task<T> ExecuteReadCommittedAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);

    /// <summary>トランザクションなしの短命 UoW で読み取りのみ実行する。</summary>
    Task ExecuteReadOnlyAsync(
        Func<ICoreUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    /// <summary>トランザクションなしの短命 UoW で読み取りのみ実行し、結果を返す。</summary>
    Task<T> ExecuteReadOnlyAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default);
}
