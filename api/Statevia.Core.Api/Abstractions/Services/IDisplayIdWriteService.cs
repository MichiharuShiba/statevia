using Statevia.Core.Api.Abstractions.Persistence;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// 表示用 ID の書き込み（UoW 参加型）。
/// </summary>
internal interface IDisplayIdWriteService
{
    /// <summary>
    /// 新しい表示用 ID を <paramref name="uow"/> に追加する。SaveChanges は呼び出し側。
    /// 衝突時は再生成する。
    /// </summary>
    Task<string> AllocateAsync(ICoreUnitOfWork uow, string kind, Guid uuid, CancellationToken ct = default);
}
