namespace Statevia.Core.Application.Services;

/// <inheritdoc />
internal sealed class ExecutionSecuritySnapshotFactory : IExecutionSecuritySnapshotFactory
{
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IPrincipalDataAccess _principalDataAccess;
    private readonly ICoreTransactionExecutor _executor;
    private readonly IDefinitionRepository _definitions;
    private readonly IProjectRepository _projects;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ExecutionSecuritySnapshotFactory(
        ITenantContextAccessor tenantContext,
        IPrincipalDataAccess principalDataAccess,
        ICoreTransactionExecutor executor,
        IDefinitionRepository definitions,
        IProjectRepository projects)
    {
        _tenantContext = tenantContext;
        _principalDataAccess = principalDataAccess;
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
        if (_tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var principal = await _principalDataAccess
            .FindPrincipalAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        PrincipalIdentityAuthorization.EnsureLive(principal);

        var effectiveKeys = await ResolveEffectivePermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        var permissionSetHash = PermissionSetHash.Compute(effectiveKeys);

        var isTenantAdmin = await _principalDataAccess
            .IsTenantAdminAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        var groupSnapshots = await _principalDataAccess
            .GetGroupSnapshotsForPrincipalAsync(principalId, cancellationToken)
            .ConfigureAwait(false);

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
        if (_tenantContext.EffectivePermissionKeys is { } fixedKeys)
            return fixedKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        var expanded = await _principalDataAccess
            .ExpandPrincipalPermissionKeysAsync(principalId, cancellationToken)
            .ConfigureAwait(false);
        return expanded.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
}
