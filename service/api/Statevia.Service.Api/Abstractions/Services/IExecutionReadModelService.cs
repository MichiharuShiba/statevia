using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>
/// 実行読み取りモデル（Execution Read Model）の取得契約。
/// </summary>
public interface IExecutionReadModelService
{
    /// <summary>
    /// 表示 ID で実行読み取りモデルを取得する。テナントは <see cref="Statevia.Core.Application.Contracts.Security.ITenantContext"/> から解決する。
    /// </summary>
    /// <param name="id">ワークフロー表示 ID または UUID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task<ExecutionReadModel> GetByDisplayIdAsync(string id, CancellationToken ct = default);
}
