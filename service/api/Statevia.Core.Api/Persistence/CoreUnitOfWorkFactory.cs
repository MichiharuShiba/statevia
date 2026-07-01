using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;

namespace Statevia.Core.Api.Persistence;

/// <summary>
/// <see cref="ICoreUnitOfWorkFactory"/> の EF Core 実装。
/// </summary>
internal sealed class CoreUnitOfWorkFactory : ICoreUnitOfWorkFactory
{
    private readonly IDbContextFactory<CoreDbContext> _dbFactory;

    /// <summary>
    /// 新しいインスタンスを初期化する。
    /// </summary>
    public CoreUnitOfWorkFactory(IDbContextFactory<CoreDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<ICoreUnitOfWork> CreateAsync(CancellationToken cancellationToken = default)
    {
        var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return new CoreUnitOfWork(db);
    }
}
