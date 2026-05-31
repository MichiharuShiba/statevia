using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary><see cref="TenantAdminAuthorization"/> の検証。</summary>
public sealed class TenantAdminAuthorizationTests
{
    /// <summary>テナント管理者ユーザーは true を返す。</summary>
    [Fact]
    public async Task IsTenantAdminAsync_AdminUser_ReturnsTrue()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var principalId = await SecurityTestSeed.SeedUserAsync(
            database,
            "admin@example.com",
            "password",
            isTenantAdmin: true);
        var authorization = new TenantAdminAuthorization(new PlatformDataAccess(database.Factory));

        // Act
        var isAdmin = await authorization.IsTenantAdminAsync(principalId, CancellationToken.None);

        // Assert
        Assert.True(isAdmin);
    }
}
