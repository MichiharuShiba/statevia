using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence;

/// <summary><see cref="CoreTransactionExecutor"/> の検証。</summary>
public sealed class CoreTransactionExecutorTests
{
    /// <summary>ReadCommitted でコミットした変更が永続化される。</summary>
    [Fact]
    public async Task ExecuteReadCommittedAsync_CommitsChanges()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var executor = new CoreTransactionExecutor(new CoreUnitOfWorkFactory(database.Factory));
        var definitionId = Guid.NewGuid();

        // Act
        await executor.ExecuteReadCommittedAsync(
            async (uow, ct) =>
            {
                uow.GetDb().WorkflowDefinitions.Add(new WorkflowDefinitionRow
                {
                    DefinitionId = definitionId,
                    TenantId = TestTenantIds.DefaultTenantId,
                    Name = "executor-commit",
                    SourceYaml = "name: executor-commit",
                    CompiledJson = "{}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        await using var verify = await database.Factory.CreateDbContextAsync();
        var row = await verify.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId);
        Assert.NotNull(row);
    }

    /// <summary>ReadCommitted で例外時はロールバックする。</summary>
    [Fact]
    public async Task ExecuteReadCommittedAsync_OnException_RollsBack()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var executor = new CoreTransactionExecutor(new CoreUnitOfWorkFactory(database.Factory));
        var definitionId = Guid.NewGuid();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteReadCommittedAsync<int>(
                async (uow, ct) =>
                {
                    uow.GetDb().WorkflowDefinitions.Add(new WorkflowDefinitionRow
                    {
                        DefinitionId = definitionId,
                        TenantId = TestTenantIds.DefaultTenantId,
                        Name = "executor-rollback",
                        SourceYaml = "name: executor-rollback",
                        CompiledJson = "{}",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    await Task.CompletedTask;
                    throw new InvalidOperationException("fail");
                },
                CancellationToken.None));

        // Assert
        await using var verify = await database.Factory.CreateDbContextAsync();
        var row = await verify.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId);
        Assert.Null(row);
    }

    /// <summary>ReadCommitted（戻り値あり）は結果を返す。</summary>
    [Fact]
    public async Task ExecuteReadCommittedAsync_WithResult_ReturnsValue()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var executor = new CoreTransactionExecutor(new CoreUnitOfWorkFactory(database.Factory));

        // Act
        var count = await executor.ExecuteReadCommittedAsync(
            (uow, ct) => uow.GetDb().Tenants.CountAsync(ct),
            CancellationToken.None);

        // Assert
        Assert.True(count >= 1);
    }

    /// <summary>ReadOnly は SaveChanges せず読み取りのみ行う。</summary>
    [Fact]
    public async Task ExecuteReadOnlyAsync_ReturnsQueryResult()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var executor = new CoreTransactionExecutor(new CoreUnitOfWorkFactory(database.Factory));
        var definitionId = Guid.NewGuid();

        // Act
        var count = await executor.ExecuteReadOnlyAsync(
            async (uow, ct) =>
            {
                uow.GetDb().WorkflowDefinitions.Add(new WorkflowDefinitionRow
                {
                    DefinitionId = definitionId,
                    TenantId = TestTenantIds.DefaultTenantId,
                    Name = "readonly",
                    SourceYaml = "name: readonly",
                    CompiledJson = "{}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                return await uow.GetDb().WorkflowDefinitions.CountAsync(ct);
            },
            CancellationToken.None);

        // Assert
        Assert.True(count >= 0);
        await using var verify = await database.Factory.CreateDbContextAsync();
        var row = await verify.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId);
        Assert.Null(row);
    }

    /// <summary>ReadOnly（戻り値なし）オーバーロードを実行できる。</summary>
    [Fact]
    public async Task ExecuteReadOnlyAsync_VoidOverload_Completes()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var executor = new CoreTransactionExecutor(new CoreUnitOfWorkFactory(database.Factory));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            executor.ExecuteReadOnlyAsync(
                async (uow, ct) => { _ = await uow.GetDb().Tenants.CountAsync(ct); },
                CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }
}
