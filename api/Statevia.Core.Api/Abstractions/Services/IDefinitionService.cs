using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Controllers;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// ワークフロー定義の登録・更新・一覧・取得のユースケース契約。
/// </summary>
public interface IDefinitionService
{
    /// <summary>定義を新規登録する。</summary>
    Task<DefinitionResponse> CreateAsync(string tenantId, CreateDefinitionRequest request, CancellationToken ct);

    /// <summary>表示 ID または UUID で定義を更新する。</summary>
    Task<DefinitionResponse> UpdateAsync(string tenantId, string idOrUuid, UpdateDefinitionRequest request, CancellationToken ct);

    /// <summary>テナント内の定義を全件（非ページング）で返す。</summary>
    Task<List<DefinitionResponse>> ListAsync(string tenantId, CancellationToken ct);

    /// <summary>
    /// ページング付き一覧を返す。
    /// </summary>
    Task<PagedResult<DefinitionResponse>> ListPagedAsync(
        string tenantId,
        DefinitionListQuery query,
        CancellationToken ct);

    /// <summary>表示 ID または UUID で単一定義を取得する。</summary>
    Task<DefinitionResponse> GetAsync(string tenantId, string idOrUuid, CancellationToken ct);
}
