namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// ワークフロー定義の登録・更新・一覧・取得・検証のユースケース契約。
/// </summary>
public interface IDefinitionService
{
    /// <summary>定義を新規登録する。</summary>
    Task<DefinitionResponse> CreateAsync(CreateDefinitionRequest request, CancellationToken ct);

    /// <summary>定義 YAML を検証・コンパイルし、catalog へ保存せず結果だけ返す。</summary>
    /// <param name="request">YAML と任意の定義名。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>成功時は <c>valid: true</c> と実効名。失敗は 422 用例外。</returns>
    Task<ValidateDefinitionResponse> ValidateAsync(ValidateDefinitionRequest request, CancellationToken ct);

    /// <summary>表示 ID または UUID で定義を更新する。</summary>
    Task<DefinitionResponse> UpdateAsync(string idOrUuid, UpdateDefinitionRequest request, CancellationToken ct);

    /// <summary>ページング付き一覧を返す。</summary>
    Task<PagedResult<DefinitionResponse>> ListPagedAsync(
        Persistence.DefinitionListPageQuery query,
        CancellationToken ct);

    /// <summary>表示 ID または UUID で単一定義を取得する。</summary>
    Task<DefinitionResponse> GetAsync(string idOrUuid, CancellationToken ct);

    /// <summary>catalog から定義を論理削除する。</summary>
    Task DeleteAsync(string idOrUuid, CancellationToken ct);

    /// <summary>削除済み定義を catalog に復元する。</summary>
    Task<DefinitionResponse> RestoreAsync(string idOrUuid, CancellationToken ct);
}
