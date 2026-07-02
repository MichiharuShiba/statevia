using System.Data;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 1 つのデータベースセッションのライフタイムとトランザクション制御。
/// </summary>
public interface ICoreUnitOfWork : IAsyncDisposable
{
    /// <summary>この UoW が保持するデータベースセッション（実装は EF Core DbContext）。</summary>
    ICoreDatabase Db { get; }

    /// <summary>トランザクションを開始する（二重開始は不可）。</summary>
    Task BeginTransactionAsync(IsolationLevel level, CancellationToken cancellationToken);

    /// <summary>変更をセッションに反映する（コミットは <see cref="CommitAsync"/>）。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>開始済みトランザクションをコミットする。</summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>開始済みトランザクションをロールバックする。</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
