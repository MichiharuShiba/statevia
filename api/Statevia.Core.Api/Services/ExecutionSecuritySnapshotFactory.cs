using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Infrastructure.Security;

namespace Statevia.Core.Api.Services;

/// <inheritdoc />
internal sealed class ExecutionSecuritySnapshotFactory : IExecutionSecuritySnapshotFactory
{
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IPlatformDataAccess _platformDataAccess;
    private readonly ICoreTransactionExecutor _executor;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectRepository _projects;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ExecutionSecuritySnapshotFactory(
        ITenantContextAccessor tenantContext,
        IPlatformDataAccess platformDataAccess,
        ICoreTransactionExecutor executor,
        IDefinitionRepository definitions,
        IProjectRepository projects)
    {
        _tenantContext = tenantContext;
        _platformDataAccess = platformDataAccess;
        _executor = executor;
        _definitions = definitions;
        _projects = projects;
    }

    /// <inheritdoc />
    public async Task<ExecutionSecuritySnapshot> CaptureForStartAsync(
        Guid tenantId,
        Guid definitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        // Start 発行者（Execution Owner）をリクエスト文脈から解決する。
        if (_tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var principal = await _platformDataAccess
            .FindPrincipalAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        // Identity は常に Live。無効・論理削除 Principal は Start 不可。
        PrincipalIdentityAuthorization.EnsureLive(principal);

        // Owner 経路の Snapshot 認可に使う semantic permission 集合（正規化済み）と整合ハッシュ。
        var effectiveKeys = await ResolveEffectivePermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        var permissionSetHash = PermissionSetHash.Compute(effectiveKeys);

        // 監査用 authorizationContext。認可判定の正本は effectiveKeys / evaluationMode。
        var isTenantAdmin = await _platformDataAccess
            .IsTenantAdminAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        var groupSnapshots = await _platformDataAccess
            .GetGroupSnapshotsForPrincipalAsync(principalId, cancellationToken)
            .ConfigureAwait(false);

        // 定義が属する project と Start 時点の project ロールを read-only tx で解決する。
        // group / isTenantAdmin は Platform 参照済みのためここでは組み立てのみ。
        var authorizationContext = await _executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await _definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                var projectRole = await _projects
                    .ResolveEffectiveRoleAsync(uow, tenantId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
                if (projectRole is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                return new AuthorizationContextSnapshot
                {
                    ProjectId = projectId.Value,
                    ProjectRole = ProjectAccessRolePolicy.ToStorageValue(projectRole.Value),
                    GroupSnapshots = groupSnapshots,
                    IsTenantAdmin = isTenantAdmin
                };
            },
            cancellationToken).ConfigureAwait(false);

        // Start tx コミット時刻とともに不変スナップショットとして確定する。
        return new ExecutionSecuritySnapshot
        {
            TenantId = tenantId,
            StartedByPrincipalId = principalId,
            PrincipalType = PrincipalTypeLabels.ToSnapshotLabel(principal.PrincipalType),
            EffectivePermissionKeys = effectiveKeys,
            PermissionSetHash = permissionSetHash,
            AuthorizationContext = authorizationContext,
            EvaluationMode = SecurityEvaluationMode.Snapshot,
            CapturedAt = capturedAt
        };
    }

    private async Task<IReadOnlyList<string>> ResolveEffectivePermissionKeysAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        // API キー経路ではミドルウェアが交差済み scopes を固定集合として渡す。
        if (_tenantContext.EffectivePermissionKeys is { } fixedKeys)
            return fixedKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        // ユーザー / サービスアカウントはグループ展開 + テナント管理者フラグから展開する。
        var expanded = await _platformDataAccess
            .ExpandPrincipalPermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        return expanded.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
}
