using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence;

public sealed class CoreUnitOfWorkTests
{
    /// <summary>
    /// ReadCommitted でコミットした変更が永続化される。
    /// </summary>
    [Fact]
    public async Task CommitAsync_PersistsChanges_OnReadCommittedTransaction()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var factory = new TestCoreUnitOfWorkFactory(database.Factory);
        var tenantId = TestTenantIds.DefaultTenantId;
        var definitionId = Guid.NewGuid();

        // Act
        await using (var uow = await factory.CreateAsync())
        {
            await uow.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, CancellationToken.None);
            uow.GetDb().WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = definitionId,
                TenantId = tenantId,
                Name = "uow-test",
                SourceYaml = "name: uow-test",
                CompiledJson = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await uow.SaveChangesAsync(CancellationToken.None);
            await uow.CommitAsync(CancellationToken.None);
        }

        // Assert
        await using var verify = await database.Factory.CreateDbContextAsync();
        var row = await verify.WorkflowDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId && x.TenantId == tenantId);
        Assert.NotNull(row);
    }

    /// <summary>
    /// ロールバック後は変更が残らない。
    /// </summary>
    [Fact]
    public async Task RollbackAsync_DoesNotPersistChanges()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var factory = new TestCoreUnitOfWorkFactory(database.Factory);
        var definitionId = Guid.NewGuid();

        // Act
        await using (var uow = await factory.CreateAsync())
        {
            await uow.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, CancellationToken.None);
            uow.GetDb().WorkflowDefinitions.Add(new WorkflowDefinitionRow
            {
                DefinitionId = definitionId,
                TenantId = TestTenantIds.DefaultTenantId,
                Name = "rollback-test",
                SourceYaml = "name: rollback",
                CompiledJson = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await uow.SaveChangesAsync(CancellationToken.None);
            await uow.RollbackAsync(CancellationToken.None);
        }

        // Assert
        await using var verify = await database.Factory.CreateDbContextAsync();
        var row = await verify.WorkflowDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.DefinitionId == definitionId);
        Assert.Null(row);
    }
}
