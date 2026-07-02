using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Service.Api.Persistence;

/// <summary><see cref="ICoreUnitOfWork"/> から EF Core 実装を取得する。</summary>
internal static class CoreUnitOfWorkExtensions
{
    /// <summary>EF Core <see cref="CoreDbContext"/> を返す。</summary>
    internal static CoreDbContext GetDb(this ICoreUnitOfWork uow) =>
        (CoreDbContext)uow.Db;
}
