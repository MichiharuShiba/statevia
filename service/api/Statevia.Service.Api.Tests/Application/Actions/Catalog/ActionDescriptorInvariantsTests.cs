using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Service.Api.Application.Actions.Catalog;

namespace Statevia.Service.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="ActionDescriptorInvariants"/> の単体テスト。</summary>
public sealed class ActionDescriptorInvariantsTests
{
    private static ActionDescriptor CreateBaseDescriptor(ActionVisibility visibility, string? ownerTenantId) =>
        new()
        {
            ActionId = "test.action",
            ModuleId = "test.module",
            Version = "1.0.0",
            Visibility = visibility,
            OwnerTenantId = ownerTenantId,
        };

    /// <summary>Builtin で OwnerTenantId が設定されている場合は拒否する。</summary>
    [Fact]
    public void Validate_BuiltinWithOwnerTenantId_Throws()
    {
        // Arrange
        var descriptor = CreateBaseDescriptor(ActionVisibility.Builtin, "11111111-1111-1111-1111-111111111111");

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionDescriptorInvariants.Validate(descriptor));
        Assert.Contains("OwnerTenantId must be null", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Marketplace で OwnerTenantId が設定されている場合は拒否する。</summary>
    [Fact]
    public void Validate_MarketplaceWithOwnerTenantId_Throws()
    {
        // Arrange
        var descriptor = CreateBaseDescriptor(ActionVisibility.Marketplace, "11111111-1111-1111-1111-111111111111");

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionDescriptorInvariants.Validate(descriptor));
        Assert.Contains("OwnerTenantId must be null", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Organization Visibility は Phase 1 では未対応。</summary>
    [Fact]
    public void Validate_Organization_Throws()
    {
        // Arrange
        var descriptor = CreateBaseDescriptor(ActionVisibility.Organization, null);

        // Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => ActionDescriptorInvariants.Validate(descriptor));
        Assert.Contains("Organization is not supported", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>必須フィールドが揃っていれば Tenant Visibility を許可する。</summary>
    [Fact]
    public void Validate_TenantWithOwner_DoesNotThrow()
    {
        // Arrange
        var descriptor = CreateBaseDescriptor(
            ActionVisibility.Tenant,
            "11111111-1111-1111-1111-111111111111");

        // Act / Assert
        ActionDescriptorInvariants.Validate(descriptor);
    }
}
