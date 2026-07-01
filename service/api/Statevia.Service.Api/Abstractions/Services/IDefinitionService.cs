using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>
/// ワークフロー定義の登録・更新・一覧・取得のユースケース契約。
/// </summary>
public interface IDefinitionService
{
    /// <summary>定義を新規登録する。</summary>
    Task<DefinitionResponse> CreateAsync(CreateDefinitionRequest request, CancellationToken ct);

    /// <summary>表示 ID または UUID で定義を更新する。</summary>
    Task<DefinitionResponse> UpdateAsync(string idOrUuid, UpdateDefinitionRequest request, CancellationToken ct);

    /// <summary>
    /// ページング付き一覧を返す。<paramref name="query"/>.<see cref="DefinitionListQuery.Limit"/> は必須。
    /// </summary>
    Task<PagedResult<DefinitionResponse>> ListPagedAsync(
        DefinitionListQuery query,
        CancellationToken ct);

    /// <summary>表示 ID または UUID で単一定義を取得する。</summary>
    Task<DefinitionResponse> GetAsync(string idOrUuid, CancellationToken ct);
}
