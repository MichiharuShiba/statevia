using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Persistence.Repositories;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Services;

/// <summary><see cref="ProjectAuthorizationService"/> の検証。</summary>
public sealed class ProjectAuthorizationServiceTests
{
    /// <summary>Reader 付与テナントは読み取りを許可する。</summary>
    [Fact]
    public async Task EnsureCanReadAsync_WithReaderRole_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var granteeTenantId = Guid.NewGuid();
        var projectId = await SeedSharedProjectAsync(db, granteeTenantId, ProjectAccessRole.Reader);
        var service = new ProjectAuthorizationService(new ProjectRepository());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanReadAsync(uow, granteeTenantId, projectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Reader のみのテナントが Execute を試行すると 403 になる。</summary>
    [Fact]
    public async Task EnsureCanExecuteAsync_ReaderOnly_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var granteeTenantId = Guid.NewGuid();
        var projectId = await SeedSharedProjectAsync(db, granteeTenantId, ProjectAccessRole.Reader);
        var service = new ProjectAuthorizationService(new ProjectRepository());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EnsureCanExecuteAsync(uow, granteeTenantId, projectId, default));

        // Assert
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>Executor 付与テナントは Execute を許可する。</summary>
    [Fact]
    public async Task EnsureCanExecuteAsync_WithExecutorRole_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var granteeTenantId = Guid.NewGuid();
        var projectId = await SeedSharedProjectAsync(db, granteeTenantId, ProjectAccessRole.Executor);
        var service = new ProjectAuthorizationService(new ProjectRepository());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanExecuteAsync(uow, granteeTenantId, projectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Publisher 付与テナントは publish を許可する。</summary>
    [Fact]
    public async Task EnsureCanPublishAsync_WithPublisherRole_DoesNotThrow()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var granteeTenantId = Guid.NewGuid();
        var projectId = await SeedSharedProjectAsync(db, granteeTenantId, ProjectAccessRole.Publisher);
        var service = new ProjectAuthorizationService(new ProjectRepository());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var exception = await Record.ExceptionAsync(() =>
            service.EnsureCanPublishAsync(uow, granteeTenantId, projectId, default));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Executor のみのテナントが publish を試行すると 403 になる。</summary>
    [Fact]
    public async Task EnsureCanPublishAsync_ExecutorOnly_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var granteeTenantId = Guid.NewGuid();
        var projectId = await SeedSharedProjectAsync(db, granteeTenantId, ProjectAccessRole.Executor);
        var service = new ProjectAuthorizationService(new ProjectRepository());
        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EnsureCanPublishAsync(uow, granteeTenantId, projectId, default));

        // Assert
        Assert.Equal("PROJECT_ACCESS_DENIED", ex.Code);
    }

    /// <summary>未付与テナントは存在秘匿の 404 になる。</summary>
    [Fact]
    public async Task EnsureCanReadAsync_NoAccess_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var service = new ProjectAuthorizationService(new ProjectRepository());
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
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.EnsureCanReadAsync(uow, otherTenantId, projectId, default));

        // Assert
        Assert.NotNull(ex);
    }

    private static async Task<Guid> SeedSharedProjectAsync(
        SqliteTestDatabase db,
        Guid granteeTenantId,
        ProjectAccessRole grantRole)
    {
        var ownerTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

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
            seed.Tenants.Add(new TenantRow
            {
                TenantId = granteeTenantId,
                TenantKey = $"grantee-{granteeTenantId:N}",
                DisplayName = "Grantee",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
            ProjectTestData.GrantAccess(seed, projectId, granteeTenantId, grantRole);
            await seed.SaveChangesAsync();
        }

        return projectId;
    }
}
