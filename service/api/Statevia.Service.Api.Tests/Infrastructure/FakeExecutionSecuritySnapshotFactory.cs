using Statevia.Service.Api.Application.Security;

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
}
