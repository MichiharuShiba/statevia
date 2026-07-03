using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Security;

/// <summary><see cref="TenantContextExtensions"/> の検証。</summary>
public sealed class TenantContextExtensionsTests
{
    /// <summary>解決済み文脈から内部 UUID を取得できる。</summary>
    [Fact]
    public void GetRequiredTenantId_WhenResolved_ReturnsInternalId()
    {
        // Arrange
        ITenantContext context = new FixedTenantContextAccessor(TestTenantIds.DefaultContext);

        // Act
        var tenantId = context.GetRequiredTenantId();

        // Assert
        Assert.Equal(TestTenantIds.DefaultTenantId, tenantId);
    }

    /// <summary>未解決文脈は InvalidOperationException になる。</summary>
    [Fact]
    public void GetRequiredTenantId_WhenUnresolved_ThrowsInvalidOperationException()
    {
        // Arrange
        ITenantContext context = NullTenantContextAccessor.Instance;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => context.GetRequiredTenantId());
        Assert.Contains("not resolved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>null 参照は ArgumentNullException になる。</summary>
    [Fact]
    public void GetRequiredTenantId_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        ITenantContext? context = null;
        Assert.Throws<ArgumentNullException>(() => context!.GetRequiredTenantId());
    }

    /// <summary>解決済み文脈から tenant_key を取得できる。</summary>
    [Fact]
    public void GetRequiredTenantKey_WhenResolved_ReturnsTenantKey()
    {
        // Arrange
        ITenantContext context = new FixedTenantContextAccessor(TestTenantIds.DefaultContext);

        // Act
        var tenantKey = context.GetRequiredTenantKey();

        // Assert
        Assert.Equal("default", tenantKey);
    }

    /// <summary>未解決文脈は InvalidOperationException になる。</summary>
    [Fact]
    public void GetRequiredTenantKey_WhenUnresolved_ThrowsInvalidOperationException()
    {
        // Arrange
        ITenantContext context = NullTenantContextAccessor.Instance;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => context.GetRequiredTenantKey());
        Assert.Contains("not resolved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>null 参照は ArgumentNullException になる。</summary>
    [Fact]
    public void GetRequiredTenantKey_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        ITenantContext? context = null;
        Assert.Throws<ArgumentNullException>(() => context!.GetRequiredTenantKey());
    }
}
