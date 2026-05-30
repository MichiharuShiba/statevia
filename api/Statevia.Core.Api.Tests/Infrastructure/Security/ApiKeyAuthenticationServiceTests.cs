using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Infrastructure.Security;

/// <summary><see cref="ApiKeyAuthenticationService"/> の検証。</summary>
public sealed class ApiKeyAuthenticationServiceTests
{
    /// <summary>空の API キーは null を返す。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_EmptyKey_ReturnsNull(string plainKey)
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>未登録 API キーは null を返す。</summary>
    [Fact]
    public async Task ValidateAsync_UnknownKey_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync("unknown-key", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>有効 API キーは検証結果を返し last_used_at を更新する。</summary>
    [Fact]
    public async Task ValidateAsync_ValidKey_ReturnsResultAndTouchesLastUsed()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, apiKeyId, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestTenantIds.DefaultInternalId, result.Tenant.TenantId);
        Assert.Contains("executions.read", result.EffectiveScopes);

        await using var db = database.Factory.CreateDbContext();
        var row = await db.ApiKeys.IgnoreQueryFilters().SingleAsync(k => k.ApiKeyId == apiKeyId);
        Assert.NotNull(row.LastUsedAt);
    }

    /// <summary>期限切れ API キーは null を返す。</summary>
    [Fact]
    public async Task ValidateAsync_ExpiredKey_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(
            database,
            expiresAt: DateTime.UtcNow.AddHours(-1));
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>非アクティブ Principal の API キーは null を返す。</summary>
    [Fact]
    public async Task ValidateAsync_InactivePrincipal_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(database, principalIsActive: false);
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>論理削除済み Principal の API キーは null を返す。</summary>
    [Fact]
    public async Task ValidateAsync_DeletedPrincipal_ReturnsNull()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(
            database,
            principalDeletedAt: DateTime.UtcNow);
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>不正 JSON の allowed_scopes は空集合として扱う。</summary>
    [Fact]
    public async Task ValidateAsync_InvalidScopesJson_ReturnsEmptyEffectiveScopes()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(
            database,
            allowedScopesJson: "not-json");
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.EffectiveScopes);
    }

    /// <summary>null 配列の allowed_scopes は空集合として扱う。</summary>
    [Fact]
    public async Task ValidateAsync_NullScopesArray_ReturnsEmptyEffectiveScopes()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(
            database,
            allowedScopesJson: "null");
        var service = CreateService(database);

        // Act
        var result = await service.ValidateAsync(plainKey, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.EffectiveScopes);
    }

    private static ApiKeyAuthenticationService CreateService(SqliteTestDatabase database) =>
        new(new PlatformDataAccess(database.Factory));
}
