using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Services;

/// <summary><see cref="IProjectAuthorizationService"/> — project_accesses + オーナーを truth とする。</summary>
internal sealed class ProjectAuthorizationService : IProjectAuthorizationService
{
    private readonly IProjectRepository _projects;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ProjectAuthorizationService(IProjectRepository projects) => _projects = projects;

    /// <inheritdoc />
    public async Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        await EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Reader,
            forbiddenWhenBelowMinimum: false,
            ct).ConfigureAwait(false);

    /// <inheritdoc />
    public Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Executor,
            forbiddenWhenBelowMinimum: true,
            ct);

    /// <inheritdoc />
    public Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureMinimumRoleAsync(
            uow,
            tenantId,
            projectId,
            ProjectAccessRole.Publisher,
            forbiddenWhenBelowMinimum: true,
            ct);

    private async Task EnsureMinimumRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        ProjectAccessRole minimumRole,
        bool forbiddenWhenBelowMinimum,
        CancellationToken ct)
    {
        var effectiveRole = await _projects
            .ResolveEffectiveRoleAsync(uow, tenantId, projectId, ct)
            .ConfigureAwait(false);

        if (effectiveRole is null)
            throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);

        if (ProjectAccessRolePolicy.MeetsMinimum(effectiveRole.Value, minimumRole))
            return;

        if (forbiddenWhenBelowMinimum && effectiveRole.Value >= ProjectAccessRole.Reader)
            throw new ForbiddenException(ProjectAuthorizationMessages.InsufficientProjectRole, "PROJECT_ACCESS_DENIED");

        throw new NotFoundException(ProjectAuthorizationMessages.ProjectNotFound);
    }
}

/// <summary>project 認可メッセージ。</summary>
internal static class ProjectAuthorizationMessages
{
    /// <summary>越境・未付与時（存在秘匿）。</summary>
    public const string ProjectNotFound = "Project not found";

    /// <summary>ロール不足（Reader が Executor 操作を試行等）。</summary>
    public const string InsufficientProjectRole = "Insufficient project access role.";
}
