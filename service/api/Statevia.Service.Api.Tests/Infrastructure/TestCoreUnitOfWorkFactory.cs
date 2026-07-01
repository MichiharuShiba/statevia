using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Abstractions.Persistence;
using Statevia.Service.Api.Persistence;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>
/// テスト用 <see cref="ICoreUnitOfWorkFactory"/>。
/// </summary>
internal sealed class TestCoreUnitOfWorkFactory : ICoreUnitOfWorkFactory
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    public TestCoreUnitOfWorkFactory(IDbContextFactory<CoreDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<ICoreUnitOfWork> CreateAsync(CancellationToken cancellationToken = default)
    {
        var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return new CoreUnitOfWork(db);
    }
}

/// <summary>
/// テスト用 <see cref="ICoreTransactionExecutor"/>（ReadCommitted は SaveChanges のみ、実トランザクションは開始しない）。
/// </summary>
internal sealed class TestCoreTransactionExecutor : ICoreTransactionExecutor
{
    private readonly ICoreUnitOfWorkFactory _unitOfWorkFactory;

    public TestCoreTransactionExecutor(ICoreUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory;

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

    public async Task<T> ExecuteReadCommittedAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        await using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        var result = await work(uow, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

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

    public async Task<T> ExecuteReadOnlyAsync<T>(
        Func<ICoreUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        await using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        return await work(uow, cancellationToken).ConfigureAwait(false);
    }
}
