using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>projects / project_accesses 永続化。</summary>
internal interface IProjectRepository
{
    /// <summary>テナントの既定 project（slug=<c>default</c>）を取得する。無ければ作成する。</summary>
    Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid ownerTenantInternalId,
        string ownerTenantKey,
        CancellationToken ct);

    /// <summary>project に対する実効ロールを解決する（オーナーは Admin）。</summary>
    Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>指定 project へのアクセス行を追加する。</summary>
    Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid granteeTenantInternalId,
        ProjectAccessRole role,
        CancellationToken ct);
}
