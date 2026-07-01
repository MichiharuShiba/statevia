using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Infrastructure.Security;

/// <inheritdoc />
internal sealed class ExecutionMutationAuthorization : IExecutionMutationAuthorization
{
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IRuntimePermissionAuthorization _runtimeAuth;
    private readonly IPlatformDataAccess _platformDataAccess;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ExecutionMutationAuthorization(
        ITenantContextAccessor tenantContext,
        IRuntimePermissionAuthorization runtimeAuth,
        IPlatformDataAccess platformDataAccess)
    {
        _tenantContext = tenantContext;
        _runtimeAuth = runtimeAuth;
        _platformDataAccess = platformDataAccess;
    }

    /// <inheritdoc />
    public async Task EnsureMutationPermissionAsync(
        ExecutionSecuritySnapshot? snapshot,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            throw new ArgumentException("permissionKey is required.", nameof(permissionKey));

        if (_tenantContext.PrincipalId is not { } callerPrincipalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var principal = await _platformDataAccess
            .FindPrincipalAsync(callerPrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        PrincipalIdentityAuthorization.EnsureLive(principal);

        if (ShouldUseLiveAuthorization(snapshot, callerPrincipalId))
        {
            await _runtimeAuth.EnsurePermissionAsync(permissionKey, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!snapshot!.EffectivePermissionKeys.Contains(permissionKey, StringComparer.Ordinal))
            throw new ForbiddenException(
                "Insufficient permission.",
                RuntimePermissionAuthorization.PermissionDeniedCode);
    }

    /// <summary>
    /// Operator 経路・スナップショット未保存・Owner + Live のときは Live 認可へ委譲する。
    /// </summary>
    private static bool ShouldUseLiveAuthorization(
        ExecutionSecuritySnapshot? snapshot,
        Guid callerPrincipalId) =>
        snapshot is null
        || callerPrincipalId != snapshot.StartedByPrincipalId
        || snapshot.EvaluationMode != SecurityEvaluationMode.Snapshot;
}
