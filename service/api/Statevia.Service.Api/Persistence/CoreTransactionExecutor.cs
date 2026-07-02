using System.Data;

namespace Statevia.Service.Api.Persistence;

/// <summary>
/// <see cref="ICoreTransactionExecutor"/> の実装。
/// </summary>
internal sealed class CoreTransactionExecutor : ICoreTransactionExecutor
{
    private readonly ICoreUnitOfWorkFactory _unitOfWorkFactory;

    /// <summary>
    /// 新しいインスタンスを初期化する。
    /// </summary>
    public CoreTransactionExecutor(ICoreUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

    /// <inheritdoc />
    public Task ExecuteReadCommittedAsync(
        Func<ICoreUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        ExecuteReadCommittedAsync(
            async (uow, ct) =>
            {
                await work(uow, ct).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<T> ExecuteReadCommittedAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        await using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await work(uow, cancellationToken).ConfigureAwait(false);
            await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await uow.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await uow.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public Task ExecuteReadOnlyAsync(
        Func<ICoreUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        ExecuteReadOnlyAsync(
            async (uow, ct) =>
            {
                await work(uow, ct).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<T> ExecuteReadOnlyAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        await using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        return await work(uow, cancellationToken).ConfigureAwait(false);
    }
}
