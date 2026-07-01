using Statevia.Service.Api.Contracts.Actions;

namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>Action Schema HTTP API の供給サービス。</summary>
public interface IActionSchemaService
{
    /// <summary>登録 action 一覧と descriptor 概要を返す。</summary>
    ActionSchemaListResponse GetList();

    /// <summary>Playground 向け軽量 index を返す。</summary>
    ActionSchemaIndexResponse GetIndex();

    /// <summary>指定 actionId の schema 詳細を返す。未登録は <see cref="Contracts.NotFoundException"/>。</summary>
    /// <param name="actionId">canonical actionId またはエイリアス。</param>
    ActionSchemaDetailResponse GetDetail(string actionId);
}
