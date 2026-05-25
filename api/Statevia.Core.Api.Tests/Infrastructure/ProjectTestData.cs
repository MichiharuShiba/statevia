using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary>projects / project_accesses のテストデータ投入。</summary>
internal static class ProjectTestData
{
    /// <summary>オーナーテナントの既定 project を作成する。</summary>
    public static ProjectRow AddDefaultProject(
        CoreDbContext ctx,
        Guid ownerTenantInternalId,
        string ownerTenantKey,
        Guid? projectId = null)
    {
        var project = new ProjectRow
        {
            ProjectId = projectId ?? Guid.NewGuid(),
            OwnerTenantId = ownerTenantInternalId,
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
        Guid granteeTenantInternalId,
        ProjectAccessRole role)
    {
        var access = new ProjectAccessRow
        {
            ProjectId = projectId,
            TenantId = granteeTenantInternalId,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        ctx.ProjectAccesses.Add(access);
        return access;
    }
}
