using Statevia.Service.Api.Application.Security;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>project_accesses 認可 truth の結合テスト（Repository 層）。</summary>
public sealed class ProjectAuthorizationIntegrationTests
{
    /// <summary>別テナントは project_access 未付与で定義取得できない。</summary>
    [Fact]
    public async Task GetLatestByIdAsync_CrossTenantWithoutAccess_ReturnsNull()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var ownerTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var defId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var repo = TestRepositoryFactory.CreateDefinitionRepository();
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
            seed.Tenants.Add(new TenantRow
            {
                TenantId = otherTenantId,
                TenantKey = "other",
                DisplayName = "Other",
                Lifecycle = TenantLifecycle.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            ProjectTestData.AddDefaultProject(seed, ownerTenantId, "owner", projectId);
            DefinitionTestData.AddDefinitionWithVersion(seed, ownerTenantId, defId, "shared-def", projectId);
            await seed.SaveChangesAsync();
        }

        // Act
        await using var uow = await uowFactory.CreateAsync();
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            repo.GetLatestByIdAsync(uow, otherTenantId, defId, default));

        // Assert
        Assert.NotNull(ex);
    }
}
