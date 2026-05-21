using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>定義版解決の入力。</summary>
internal sealed record DefinitionVersionResolveQuery(
    Guid DefinitionId,
    int? VersionNumber,
    Guid? VersionId);

/// <summary>definitions / definition_versions 永続化。</summary>
internal interface IDefinitionRepository
{
    /// <summary>テナント内の定義と最新版を取得する。</summary>
    Task<DefinitionDetail?> GetLatestByIdAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>版 ID で版行を取得する（テナント境界付き）。</summary>
    Task<DefinitionVersionRow?> GetVersionByIdAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionVersionId,
        CancellationToken ct);

    /// <summary>定義 ID と版番号で版行を取得する。</summary>
    Task<DefinitionVersionRow?> GetVersionAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct);

    /// <summary>定義と初版を同一 UoW に追加する。</summary>
    Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct);

    /// <summary>新版を INSERT し latest_version 投影を更新する（truth 先行）。</summary>
    Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct);

    /// <summary>一覧のページング。</summary>
    Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        string tenantId,
        DefinitionListPageQuery query,
        CancellationToken ct);
}
