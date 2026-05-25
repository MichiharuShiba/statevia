using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Application.Security;

namespace Statevia.Core.Api.Persistence.Repositories;

/// <summary><see cref="IProjectRepository"/> の EF 実装。</summary>
internal sealed class ProjectRepository : IProjectRepository
{
    /// <summary>移行・初回定義作成時の既定 project slug。</summary>
    public const string DefaultProjectSlug = "default";

    /// <inheritdoc />
    public async Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid ownerTenantInternalId,
        string ownerTenantKey,
        CancellationToken ct)
    {
        var existing = await uow.Db.Projects
            .FirstOrDefaultAsync(
                p => p.OwnerTenantId == ownerTenantInternalId && p.Slug == DefaultProjectSlug,
                ct)
            .ConfigureAwait(false);

        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var project = new ProjectRow
        {
            ProjectId = Guid.NewGuid(),
            OwnerTenantId = ownerTenantInternalId,
            Slug = DefaultProjectSlug,
            DisplayName = $"{ownerTenantKey} default",
            Visibility = ProjectVisibility.Private,
            Description = "Auto-created default project",
            CreatedAt = now
        };
        uow.Db.Projects.Add(project);
        return project;
    }

    /// <inheritdoc />
    public async Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct)
    {
        var project = await uow.Db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == projectId, ct)
            .ConfigureAwait(false);

        if (project is null)
            return null;

        if (project.OwnerTenantId == tenantInternalId)
            return ProjectAccessRole.Admin;

        var access = await uow.Db.ProjectAccesses.AsNoTracking()
            .FirstOrDefaultAsync(
                pa => pa.ProjectId == projectId && pa.TenantId == tenantInternalId,
                ct)
            .ConfigureAwait(false);

        return access?.Role;
    }

    /// <inheritdoc />
    public Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid granteeTenantInternalId,
        ProjectAccessRole role,
        CancellationToken ct)
    {
        uow.Db.ProjectAccesses.Add(new ProjectAccessRow
        {
            ProjectId = projectId,
            TenantId = granteeTenantInternalId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
