using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Tests.Infrastructure.Persistence;

/// <summary>ログイン失敗ロック表のスキーマと永続化。</summary>
public sealed class LoginFailureLockPersistenceTests
{
    /// <summary>別 DbContext 再生成後もロック行と失敗行が残る。</summary>
    [Fact]
    public async Task LockRow_SurvivesNewDbContext()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var lockedUntil = new DateTime(2026, 9, 2, 12, 30, 0, DateTimeKind.Utc);
        var failedAt = new DateTime(2026, 9, 2, 12, 15, 0, DateTimeKind.Utc);
        var attemptId = Guid.Parse("01800000-0000-7000-8000-000000000001");

        await using (var write = database.Factory.CreateDbContext())
        {
            write.LoginFailureLocks.Add(new LoginFailureLockRow
            {
                TenantKey = "default",
                Username = "user",
                LockedUntil = lockedUntil
            });
            write.LoginFailureAttempts.Add(new LoginFailureAttemptRow
            {
                AttemptId = attemptId,
                TenantKey = "default",
                Username = "user",
                FailedAt = failedAt
            });
            await write.SaveChangesAsync();
        }

        // Act
        await using var read = database.Factory.CreateDbContext();
        var lockRow = await read.LoginFailureLocks.SingleAsync(
            row => row.TenantKey == "default" && row.Username == "user");
        var attemptRow = await read.LoginFailureAttempts.SingleAsync(row => row.AttemptId == attemptId);

        // Assert
        Assert.Equal(lockedUntil, DateTime.SpecifyKind(lockRow.LockedUntil!.Value, DateTimeKind.Utc));
        Assert.Equal(failedAt, DateTime.SpecifyKind(attemptRow.FailedAt, DateTimeKind.Utc));
        Assert.Equal("default", attemptRow.TenantKey);
        Assert.Equal("user", attemptRow.Username);
    }

    /// <summary>EF モデルが login_failure_locks / attempts の ER と一致する。</summary>
    [Fact]
    public void Model_MatchesLoginFailureLockEr()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        using var db = database.Factory.CreateDbContext();
        var locks = db.Model.FindEntityType(typeof(LoginFailureLockRow));
        var attempts = db.Model.FindEntityType(typeof(LoginFailureAttemptRow));

        // Act
        var lockTable = locks?.GetTableName();
        var attemptTable = attempts?.GetTableName();
        var lockPk = ColumnNames(locks, "login_failure_locks");
        var attemptPk = ColumnNames(attempts, "login_failure_attempts");
        var attemptFk = attempts?.GetForeignKeys().Single();

        // Assert
        Assert.Equal("login_failure_locks", lockTable);
        Assert.Equal("login_failure_attempts", attemptTable);
        Assert.Equal(["tenant_key", "username"], lockPk);
        Assert.Equal(["attempt_id"], attemptPk);
        Assert.Equal(typeof(LoginFailureLockRow), attemptFk?.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, attemptFk?.DeleteBehavior);
        Assert.Null(locks?.GetQueryFilter());
        Assert.Null(attempts?.GetQueryFilter());
    }

    private static string[] ColumnNames(IEntityType? entity, string tableName)
    {
        Assert.NotNull(entity);
        var store = StoreObjectIdentifier.Table(tableName);
        return [.. entity.FindPrimaryKey()!.Properties.Select(property => property.GetColumnName(store)!)];
    }
}
