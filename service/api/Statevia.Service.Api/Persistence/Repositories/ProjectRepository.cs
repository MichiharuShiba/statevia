using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Application.Security;

namespace Statevia.Service.Api.Persistence.Repositories;

/// <summary><see cref="IProjectRepository"/> の EF 実装。</summary>
internal sealed class ProjectRepository : IProjectRepository
{
    /// <summary>移行・初回定義作成時の既定 project slug。</summary>
    public const string DefaultProjectSlug = "default";

    /// <inheritdoc />
    public async Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid ownerTenantId,
        string ownerTenantKey,
        CancellationToken ct)
    {
        var existing = await uow.GetDb().Projects
            .FirstOrDefaultAsync(
                p => p.OwnerTenantId == ownerTenantId && p.Slug == DefaultProjectSlug,
                ct)
            .ConfigureAwait(false);

        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var project = new ProjectRow
        {
            ProjectId = Guid.NewGuid(),
            OwnerTenantId = ownerTenantId,
            Slug = DefaultProjectSlug,
            DisplayName = $"{ownerTenantKey} default",
            Visibility = ProjectVisibility.Private,
            Description = "Auto-created default project",
            CreatedAt = now
        };
        uow.GetDb().Projects.Add(project);
        return project;
    }

    /// <inheritdoc />
    public async Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        var project = await uow.GetDb().Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
            .ConfigureAwait(false);

        if (project is null)
            return null;

        if (project.OwnerTenantId == tenantId)
            return ProjectAccessRole.Admin;

        var access = await uow.GetDb().ProjectAccesses.AsNoTracking()
            .FirstOrDefaultAsync(
                pa => pa.ProjectId == projectId && pa.TenantId == tenantId,
                ct)
            .ConfigureAwait(false);

        return access?.Role;
    }

    /// <inheritdoc />
    public Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid granteeTenantId,
        ProjectAccessRole role,
        CancellationToken ct)
    {
        uow.GetDb().ProjectAccesses.Add(new ProjectAccessRow
        {
            ProjectId = projectId,
            TenantId = granteeTenantId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
