using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Persistence;

/// <summary><c>users</c> のテナント内 username / email 一意制約。</summary>
public sealed class UserRowConstraintTests
{
    /// <summary>同一テナントの重複 username は保存に失敗する。</summary>
    [Fact]
    public async Task SaveChanges_DuplicateUsernameSameTenant_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var db = database.Factory.CreateDbContext();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops", "a@example.com"));
        await db.SaveChangesAsync();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops", "b@example.com"));

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>テナントが違えば同一 username を保存できる。</summary>
    [Fact]
    public async Task SaveChanges_SameUsernameDifferentTenant_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var db = database.Factory.CreateDbContext();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops", "a@example.com"));
        db.Users.Add(CreateUser(TestTenantIds.T1TenantId, "ops", "b@example.com"));

        // Act
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(2, await db.Users.CountAsync(u => u.Username == "ops"));
    }

    /// <summary>同一テナントの重複 email は保存に失敗する。</summary>
    [Fact]
    public async Task SaveChanges_DuplicateEmailSameTenant_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var db = database.Factory.CreateDbContext();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops-a", "ops@example.com"));
        await db.SaveChangesAsync();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops-b", "ops@example.com"));

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    /// <summary>テナントが違えば同一 email を保存できる。</summary>
    [Fact]
    public async Task SaveChanges_SameEmailDifferentTenant_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var db = database.Factory.CreateDbContext();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops-a", "ops@example.com"));
        db.Users.Add(CreateUser(TestTenantIds.T1TenantId, "ops-b", "ops@example.com"));

        // Act
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(2, await db.Users.CountAsync(u => u.Email == "ops@example.com"));
    }

    /// <summary>同一テナントで email 未設定のユーザーを複数保存できる。</summary>
    [Fact]
    public async Task SaveChanges_MultipleNullEmailsSameTenant_Succeeds()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await using var db = database.Factory.CreateDbContext();
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops-a", email: null));
        db.Users.Add(CreateUser(TestTenantIds.DefaultTenantId, "ops-b", email: null));

        // Act
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(2, await db.Users.CountAsync(u => u.Email == null));
    }

    private static UserRow CreateUser(Guid tenantId, string username, string? email)
    {
        var now = DateTime.UtcNow;
        return new UserRow
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Username = username,
            Email = email,
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
