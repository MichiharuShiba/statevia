using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence.Repositories;

/// <summary><see cref="ProjectRepository"/> の検証。</summary>
public sealed class ProjectRepositoryTests
{
    /// <summary>既定 project が無いとき slug=default を作成する。</summary>
    [Fact]
    public async Task EnsureDefaultProjectAsync_WhenMissing_CreatesDefaultProject()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var repo = new ProjectRepository();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var project = await repo.EnsureDefaultProjectAsync(uow, ownerTenantId, "owner", default);
        await uow.GetDb().SaveChangesAsync();

        // Assert
        await using var verify = db.Factory.CreateDbContext();
        var stored = await verify.Projects.SingleAsync(p => p.ProjectId == project.ProjectId);
        Assert.Equal(ProjectRepository.DefaultProjectSlug, stored.Slug);
        Assert.Equal(ownerTenantId, stored.OwnerTenantId);
        Assert.Equal(ProjectVisibility.Private, stored.Visibility);
    }

    /// <summary>既定 project が既にあるときは既存行を返す。</summary>
    [Fact]
    public async Task EnsureDefaultProjectAsync_WhenExists_ReturnsExistingProject()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var project = await repo.EnsureDefaultProjectAsync(uow, ownerTenantId, "owner", default);

        // Assert
        Assert.Equal(projectId, project.ProjectId);
    }

    /// <summary>オーナーテナントは project_access 行なしでも Admin 相当。</summary>
    [Fact]
    public async Task ResolveEffectiveRoleAsync_Owner_ReturnsAdmin()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedOwnerProject(db, ownerTenantId, projectId);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var role = await repo.ResolveEffectiveRoleAsync(uow, ownerTenantId, projectId, default);

        // Assert
        Assert.Equal(ProjectAccessRole.Admin, role);
    }

    /// <summary>付与先テナントは project_access の role を返す。</summary>
    [Fact]
    public async Task ResolveEffectiveRoleAsync_Grantee_ReturnsGrantedRole()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var granteeTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedOwnerProject(db, ownerTenantId, projectId);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = granteeTenantId,
                TenantKey = "grantee",
                DisplayName = "Grantee",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.GrantAccess(seed, projectId, granteeTenantId, ProjectAccessRole.Executor);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var role = await repo.ResolveEffectiveRoleAsync(uow, granteeTenantId, projectId, default);

        // Assert
        Assert.Equal(ProjectAccessRole.Executor, role);
    }

    /// <summary>未付与テナントは null を返す。</summary>
    [Fact]
    public async Task ResolveEffectiveRoleAsync_NoAccess_ReturnsNull()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var repo = new ProjectRepository();
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        SeedOwnerProject(db, ownerTenantId, projectId);

        await using (var seed = db.Factory.CreateDbContext())
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var role = await repo.ResolveEffectiveRoleAsync(uow, otherTenantId, projectId, default);

        // Assert
        Assert.Null(role);
    }

    private static void SeedOwnerProject(SqliteTestDatabase db, Guid ownerTenantId, Guid projectId)
    {
        using var seed = db.Factory.CreateDbContext();
        if (!seed.Tenants.Any(t => t.TenantId == ownerTenantId))
        {
            seed.Tenants.Add(new TenantRow
            {
                TenantId = ownerTenantId,
                TenantKey = "owner",
                DisplayName = "Owner",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
        seed.SaveChanges();
    }
}
