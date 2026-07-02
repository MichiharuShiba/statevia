namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <see cref="ICoreUnitOfWork"/> インスタンスを生成する。
/// </summary>
public interface ICoreUnitOfWorkFactory
{
    /// <summary>新しい UoW を作成する。呼び出し側は <see cref="IAsyncDisposable.DisposeAsync"/> で破棄する。</summary>
    Task<ICoreUnitOfWork> CreateAsync(CancellationToken cancellationToken = default);
}
