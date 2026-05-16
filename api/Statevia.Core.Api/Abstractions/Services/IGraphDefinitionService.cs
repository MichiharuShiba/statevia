using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// グラフ定義（nodes / edges）の取得契約。
/// </summary>
public interface IGraphDefinitionService
{
    /// <summary>
    /// グラフ ID（定義の display_id）とテナントでグラフ定義を取得する。
    /// </summary>
    /// <param name="graphId">グラフ ID。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<GraphDefinitionResponse> GetByGraphIdAsync(string graphId, string tenantId, CancellationToken ct = default);
}
