using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>
/// グラフ定義（nodes / edges）の取得契約。
/// </summary>
public interface IGraphDefinitionService
{
    /// <summary>
    /// グラフ ID（定義の display_id）でグラフ定義を取得する。テナントは <see cref="Statevia.Core.Application.Contracts.Security.ITenantContext"/> から解決する。
    /// </summary>
    /// <param name="graphId">グラフ ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, CancellationToken ct = default);
}
