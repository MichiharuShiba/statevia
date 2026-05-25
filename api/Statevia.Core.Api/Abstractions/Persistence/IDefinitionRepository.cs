using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>definitions / definition_versions 永続化。</summary>
internal interface IDefinitionRepository
{
    /// <summary>project 認可（Reader+）付きで定義と最新版を取得する。</summary>
    Task<DefinitionDetail?> GetLatestByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>版 ID で版行を取得する（project 認可付き）。</summary>
    Task<DefinitionVersionRow?> GetVersionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionVersionId,
        CancellationToken ct);

    /// <summary>定義 ID と版番号で版行を取得する（project 認可付き）。</summary>
    Task<DefinitionVersionRow?> GetVersionAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionId,
        int version,
        CancellationToken ct);

    /// <summary>定義の project_id を解決する（Reader 認可済み）。</summary>
    Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>定義と初版を同一 UoW に追加する。</summary>
    Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct);

    /// <summary>新版を INSERT し latest_version 投影を更新する（Publisher+ 認可）。</summary>
    Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct);

    /// <summary>アクセス可能 project 内の定義一覧（Reader+）。</summary>
    Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        DefinitionListPageQuery query,
        CancellationToken ct);
}
