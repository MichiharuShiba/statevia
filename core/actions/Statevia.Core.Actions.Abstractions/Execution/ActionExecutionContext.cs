namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Policy が Mode を決定する際の実行コンテキスト。</summary>
/// <param name="TenantId"><c>tenants.tenant_id</c> UUID 文字列。</param>
/// <param name="Environment">実行環境名（例: Development / Production）。</param>
/// <param name="DeploymentProfile">デプロイプロファイル（任意）。</param>
public sealed record ActionExecutionContext(
    string TenantId,
    string Environment,
    string? DeploymentProfile);
