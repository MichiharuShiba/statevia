namespace Statevia.Core.Api.Application.Security;

/// <summary>Start 時点の所属グループ（監査用）。</summary>
/// <param name="Id">グループ ID。</param>
/// <param name="Name">グループ名。</param>
public sealed record GroupSnapshot(Guid Id, string Name);

/// <summary>Start 時点の project / グループ文脈。</summary>
public sealed class AuthorizationContextSnapshot
{
    /// <summary>定義が属する project。</summary>
    public required Guid ProjectId { get; init; }

    /// <summary>Start 時点の project 有効ロール（storage 値）。</summary>
    public required string ProjectRole { get; init; }

    /// <summary>Start 時点の所属グループ。</summary>
    public required IReadOnlyList<GroupSnapshot> GroupSnapshots { get; init; }

    /// <summary>Start 時点の <c>is_tenant_admin</c>。</summary>
    public required bool IsTenantAdmin { get; init; }
}

/// <summary>Start 成功時に確定する実行セキュリティスナップショット。</summary>
public sealed class ExecutionSecuritySnapshot
{
    /// <summary>JSON 構造の版。初版 = 1。</summary>
    public int SnapshotVersion { get; init; } = 1;

    /// <summary>認可アルゴリズムの版。初版 = 1。</summary>
    public int SecurityModelVersion { get; init; } = 1;

    /// <summary>テナント UUID（<c>tenants.tenant_id</c>）。</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Start を発行した Principal（Execution Owner）。</summary>
    public required Guid StartedByPrincipalId { get; init; }

    /// <summary><c>User</c> / <c>ServiceAccount</c> / <c>System</c>。</summary>
    public required string PrincipalType { get; init; }

    /// <summary>Start 時点の展開済み global permission。</summary>
    public required IReadOnlyList<string> EffectivePermissionKeys { get; init; }

    /// <summary><see cref="EffectivePermissionKeys"/> の正規化 SHA-256（小文字 hex）。</summary>
    public required string PermissionSetHash { get; init; }

    /// <summary>project / グループ文脈。</summary>
    public required AuthorizationContextSnapshot AuthorizationContext { get; init; }

    /// <summary>Owner 経路に適用する評価モード。初版 tenant 既定は <see cref="SecurityEvaluationMode.Snapshot"/>。</summary>
    public SecurityEvaluationMode EvaluationMode { get; init; } = SecurityEvaluationMode.Snapshot;

    /// <summary>UTC。Start tx のコミット時刻。</summary>
    public required DateTime CapturedAt { get; init; }

    /// <summary>キャプチャ理由。初版は <c>Start</c> 固定。</summary>
    public string CaptureReason { get; init; } = "Start";
}
