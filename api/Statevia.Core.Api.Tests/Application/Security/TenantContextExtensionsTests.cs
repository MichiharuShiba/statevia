using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Persistence;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Application.Security;

/// <summary><see cref="TenantContextExtensions"/> の検証。</summary>
public sealed class TenantContextExtensionsTests
{
    /// <summary>解決済み文脈から内部 UUID を取得できる。</summary>
    [Fact]
    public void GetRequiredTenantInternalId_WhenResolved_ReturnsInternalId()
    {
        // Arrange
        ITenantContext context = new FixedTenantContextAccessor(TestTenantIds.DefaultContext);

        // Act
        var tenantInternalId = context.GetRequiredTenantInternalId();

        // Assert
        Assert.Equal(TestTenantIds.DefaultInternalId, tenantInternalId);
    }

    /// <summary>未解決文脈は InvalidOperationException になる。</summary>
    [Fact]
    public void GetRequiredTenantInternalId_WhenUnresolved_ThrowsInvalidOperationException()
    {
        // Arrange
        ITenantContext context = NullTenantContextAccessor.Instance;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => context.GetRequiredTenantInternalId());
        Assert.Contains("not resolved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>null 参照は ArgumentNullException になる。</summary>
    [Fact]
    public void GetRequiredTenantInternalId_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        ITenantContext? context = null;
        Assert.Throws<ArgumentNullException>(() => context!.GetRequiredTenantInternalId());
    }
}
