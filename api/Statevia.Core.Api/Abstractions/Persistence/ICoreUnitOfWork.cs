using System.Data;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// 1 つの <see cref="CoreDbContext"/> のライフタイムとトランザクション制御。
/// </summary>
internal interface ICoreUnitOfWork : IAsyncDisposable
{
    /// <summary>この UoW が保持する DbContext。</summary>
    CoreDbContext Db { get; }

    /// <summary>トランザクションを開始する（二重開始は不可）。</summary>
    Task BeginTransactionAsync(IsolationLevel level, CancellationToken cancellationToken);

    /// <summary>変更を DbContext に反映する（コミットは <see cref="CommitAsync"/>）。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>開始済みトランザクションをコミットする。</summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>開始済みトランザクションをロールバックする。</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
