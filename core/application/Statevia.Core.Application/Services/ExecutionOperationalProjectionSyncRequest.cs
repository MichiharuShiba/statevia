using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// <see cref="ExecutionOperationalProjectionSync"/> への同期入力。
/// </summary>
/// <param name="ExecutionId">対象 execution。</param>
/// <param name="TenantId">テナント内部 UUID（<c>tenants.tenant_id</c>）。</param>
/// <param name="Status">投影 status（Running / Completed 等）。</param>
/// <param name="Snapshot">Engine スナップショット（cursor 位置推定用）。</param>
/// <param name="GraphJson">execution_graph_snapshots と同一の JSON。</param>
/// <param name="ResumeTokenToClear">Publish / Resume 時に削除する resume_token（任意）。</param>
internal sealed record ExecutionOperationalProjectionSyncRequest(
    Guid ExecutionId,
    Guid TenantId,
    string Status,
    ExecutionSnapshot? Snapshot,
    string GraphJson,
    string? ResumeTokenToClear);
