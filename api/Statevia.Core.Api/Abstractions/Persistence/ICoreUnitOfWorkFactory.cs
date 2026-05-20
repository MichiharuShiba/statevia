namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>
/// <see cref="ICoreUnitOfWork"/> インスタンスを生成する。
/// </summary>
internal interface ICoreUnitOfWorkFactory
{
    /// <summary>新しい UoW を作成する。呼び出し側は <see cref="IAsyncDisposable.DisposeAsync"/> で破棄する。</summary>
    Task<ICoreUnitOfWork> CreateAsync(CancellationToken cancellationToken = default);
}
