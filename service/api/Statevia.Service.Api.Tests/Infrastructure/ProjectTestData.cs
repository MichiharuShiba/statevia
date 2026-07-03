
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Persistence.Repositories;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>projects / project_accesses のテストデータ投入。</summary>
internal static class ProjectTestData
{
    /// <summary>オーナーテナントの既定 project を作成する。</summary>
    public static ProjectRow AddDefaultProject(
        CoreDbContext ctx,
        Guid ownerTenantId,
        string ownerTenantKey,
        Guid? projectId = null)
    {
        var project = new ProjectRow
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            OwnerTenantId = ownerTenantId,
            Slug = ProjectRepository.DefaultProjectSlug,
            DisplayName = $"{ownerTenantKey} default",
            Visibility = ProjectVisibility.Private,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Projects.Add(project);
        return project;
    }

    /// <summary>project_accesses 行を追加する。</summary>
    public static ProjectAccessRow GrantAccess(
        CoreDbContext ctx,
        Guid projectId,
        Guid granteeTenantId,
        ProjectAccessRole role)
    {
        var access = new ProjectAccessRow
        {
            ProjectId = projectId,
            TenantId = granteeTenantId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        ctx.ProjectAccesses.Add(access);
        return access;
    }
}
