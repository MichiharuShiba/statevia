using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary><see cref="TenantBootstrap"/> の検証。</summary>
public sealed class TenantBootstrapTests
{
    /// <summary>新規テナントを作成できる。</summary>
    [Fact]
    public async Task CreateTenantAsync_NewKey_PersistsRow()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = new TenantBootstrap(database.Factory, new PlatformDataAccess(database.Factory));

        // Act
        var result = await bootstrap.CreateTenantAsync(
            "acme-corp",
            "Acme Corporation",
            skipIfExists: false,
            CancellationToken.None);

        // Assert
        Assert.True(result.Created);
        Assert.Equal("acme-corp", result.TenantKey);
        Assert.Equal("Acme Corporation", result.DisplayName);

        await using var db = database.Factory.CreateDbContext();
        var row = await db.Tenants.IgnoreQueryFilters()
            .SingleAsync(t => t.TenantKey == "acme-corp");
        Assert.Equal(result.TenantId, row.TenantId);
    }

    /// <summary>既存キーは skipIfExists で重複しない。</summary>
    [Fact]
    public async Task CreateTenantAsync_ExistingKey_SkipIfExists_DoesNotDuplicate()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = new TenantBootstrap(database.Factory, new PlatformDataAccess(database.Factory));
        await bootstrap.CreateTenantAsync("acme-corp", null, false, CancellationToken.None);

        // Act
        var result = await bootstrap.CreateTenantAsync(
            "acme-corp",
            "Other",
            skipIfExists: true,
            CancellationToken.None);

        // Assert
        Assert.False(result.Created);
        await using var db = database.Factory.CreateDbContext();
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.TenantKey == "acme-corp"));
    }

    /// <summary>ドットを含むテナントキーを作成できる。</summary>
    [Fact]
    public async Task CreateTenantAsync_DottedKey_PersistsRow()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = new TenantBootstrap(database.Factory, new PlatformDataAccess(database.Factory));

        // Act
        var result = await bootstrap.CreateTenantAsync(
            "statevia.dev",
            displayName: null,
            skipIfExists: false,
            CancellationToken.None);

        // Assert
        Assert.True(result.Created);
        Assert.Equal("statevia.dev", result.TenantKey);
        Assert.Equal("statevia.dev", result.DisplayName);
    }

    /// <summary>既存キーで skipIfExists なしは例外。</summary>
    [Fact]
    public async Task CreateTenantAsync_ExistingKey_WithoutSkip_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = new TenantBootstrap(database.Factory, new PlatformDataAccess(database.Factory));
        await bootstrap.CreateTenantAsync("acme-corp", null, false, CancellationToken.None);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.CreateTenantAsync("acme-corp", null, skipIfExists: false, CancellationToken.None));
        Assert.Contains("acme-corp", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>表示名が長すぎる場合は例外。</summary>
    [Fact]
    public async Task CreateTenantAsync_DisplayNameTooLong_Throws()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var bootstrap = new TenantBootstrap(database.Factory, new PlatformDataAccess(database.Factory));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            bootstrap.CreateTenantAsync(
                "acme-corp",
                new string('x', 257),
                skipIfExists: false,
                CancellationToken.None));
    }
}
