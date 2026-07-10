namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>definitions / definition_versions 永続化。</summary>
public interface IDefinitionRepository
{
    /// <summary>Active catalog の最新版を取得する（GET / Graph 用）。</summary>
    Task<DefinitionDetail?> GetLatestForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>Active catalog 行を変更用に取得する（publish / soft delete 前提）。</summary>
    Task<DefinitionRow?> GetLatestForMutationAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>版行を取得する（親 definition の deleted_at は bypass。実行鎖用）。</summary>
    Task<DefinitionVersionRow?> GetVersionForExecutionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct);

    /// <summary>版 ID で版行を取得する（親 definition の deleted_at は bypass）。</summary>
    Task<DefinitionVersionRow?> GetVersionForExecutionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionVersionId,
        CancellationToken ct);

    /// <summary>親 catalog が active な版行を取得する。</summary>
    Task<DefinitionVersionRow?> GetVersionForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct);

    /// <summary>削除済み catalog 行を取得する（restore 用）。</summary>
    Task<DefinitionRow?> GetDeletedCatalogEntryAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct);

    Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct);

    Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct);

    /// <summary>catalog を論理削除する。</summary>
    Task<DefinitionSoftDeleteOutcome> SoftDeleteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        DateTime deletedAt,
        CancellationToken ct);

    /// <summary>
    /// 削除済み catalog を復元し、同一 UoW 内で応答用の最新版詳細を返す。
    /// </summary>
    /// <remarks>
    /// SaveChanges 前でも追跡エンティティから詳細を組み立てる。
    /// AsNoTracking + activeOnly の再取得だと DB 上はまだ削除済みのため null になる。
    /// </remarks>
    /// <returns>復元できた場合は詳細。対象が無い場合は null。</returns>
    Task<DefinitionDetail?> RestoreAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>同一 project 内に active な slug 競合があるか。</summary>
    Task<bool> ExistsActiveSlugInProjectAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        string slug,
        Guid excludingDefinitionId,
        CancellationToken ct);
}
