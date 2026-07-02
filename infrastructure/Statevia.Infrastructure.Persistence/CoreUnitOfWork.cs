using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Infrastructure.Persistence;

/// <summary>
/// <see cref="ICoreUnitOfWork"/> の EF Core 実装。
/// </summary>
internal sealed class CoreUnitOfWork : ICoreUnitOfWork
{
    private readonly CoreDbContext _db;
    private IDbContextTransaction? _transaction;
    private bool _committed;

    /// <summary>
    /// 新しいインスタンスを初期化する。
    /// </summary>
    public CoreUnitOfWork(CoreDbContext db) => _db = db;

    /// <inheritdoc />
    public ICoreDatabase Db => _db;

    /// <inheritdoc />
    public async Task BeginTransactionAsync(IsolationLevel level, CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active on this unit of work.");
        }

        _transaction = await _db.Database.BeginTransactionAsync(level, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _committed = true;
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null && !_committed)
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        await _db.DisposeAsync().ConfigureAwait(false);
    }
}
