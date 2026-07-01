using System.Text.Json;

namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Platform 実行層へ渡す Action 実行リクエスト。</summary>
public sealed record ActionExecutionRequest
{
    /// <summary>実行インスタンス ID。</summary>
    public required string ExecutionId { get; init; }

    /// <summary>状態名。</summary>
    public required string StateName { get; init; }

    /// <summary>canonical actionId。</summary>
    public required string ActionId { get; init; }

    /// <summary><c>tenants.tenant_id</c> UUID 文字列。</summary>
    public required string TenantId { get; init; }

    /// <summary>入力（OutOfProcess 向け。InProcess は StateContext 経由でも可）。</summary>
    public JsonElement? Input { get; init; }

    /// <summary>相関 ID（任意）。</summary>
    public string? CorrelationId { get; init; }

    /// <summary>実行期限（任意）。</summary>
    public DateTimeOffset? Deadline { get; init; }
}
