using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>
/// 実行読み取りモデル（Execution Read Model）の取得契約。
/// </summary>
public interface IExecutionReadModelService
{
    /// <summary>
    /// 表示 ID とテナントで実行読み取りモデルを取得する。
    /// </summary>
    /// <param name="id">ワークフロー表示 ID または UUID。</param>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<ExecutionReadModel> GetByDisplayIdAsync(string id, string tenantId, CancellationToken ct = default);
}
