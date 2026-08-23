

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>Start 用スナップショットを最小構成で返すテスト用ファクトリ。</summary>
internal sealed class FakeExecutionSecuritySnapshotFactory : IExecutionSecuritySnapshotFactory
{
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public FakeExecutionSecuritySnapshotFactory(ITenantContextAccessor tenantContext) =>
        _tenantContext = tenantContext;

    /// <inheritdoc />
    public Task<ExecutionSecuritySnapshot> CaptureForStartAsync(
        Guid tenantId,
        Guid definitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        var principalId = _tenantContext.PrincipalId ?? Guid.NewGuid();
        var keys = new[] { WellKnownPermissionKeys.ExecutionsWrite };
        var snapshot = new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = principalId,
            PrincipalType = "User",
            EffectivePermissionKeys = keys,
            PermissionSetHash = PermissionSetHash.Compute(keys),
            AuthorizationContext = new AuthorizationContextSnapshot
            {
                ProjectId = definitionId,
                ProjectRole = "executor",
                GroupSnapshots = Array.Empty<GroupSnapshot>(),
                IsTenantAdmin = false
            },
            EvaluationMode = SecurityEvaluationMode.Snapshot,
            CapturedAt = capturedAt
        };
        return Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public Task<ExecutionSecuritySnapshot> CaptureForChildWorkflowAsync(
        ExecutionSecuritySnapshot parentSnapshot,
        Guid tenantId,
        Guid childDefinitionId,
        Guid parentDefinitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parentSnapshot);
        _ = cancellationToken;
        var ancestors = (parentSnapshot.AncestorDefinitionIds ?? [])
            .Append(parentDefinitionId)
            .Distinct()
            .ToArray();
        var snapshot = new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = parentSnapshot.StartedByPrincipalId,
            PrincipalType = parentSnapshot.PrincipalType,
            EffectivePermissionKeys = parentSnapshot.EffectivePermissionKeys,
            PermissionSetHash = parentSnapshot.PermissionSetHash,
            AuthorizationContext = new AuthorizationContextSnapshot
            {
                ProjectId = childDefinitionId,
                ProjectRole = parentSnapshot.AuthorizationContext.ProjectRole,
                GroupSnapshots = parentSnapshot.AuthorizationContext.GroupSnapshots,
                IsTenantAdmin = parentSnapshot.AuthorizationContext.IsTenantAdmin
            },
            EvaluationMode = parentSnapshot.EvaluationMode,
            CapturedAt = capturedAt,
            CaptureReason = parentSnapshot.CaptureReason,
            AncestorDefinitionIds = ancestors
        };
        return Task.FromResult(snapshot);
    }
}
