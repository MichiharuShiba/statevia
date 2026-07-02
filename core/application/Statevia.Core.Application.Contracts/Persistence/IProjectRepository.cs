using Statevia.Core.Application.Contracts.Security;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>projects / project_accesses 永続化。</summary>
public interface IProjectRepository
{
    Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid ownerTenantId,
        string ownerTenantKey,
        CancellationToken ct);

    Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid granteeTenantId,
        ProjectAccessRole role,
        CancellationToken ct);
}
