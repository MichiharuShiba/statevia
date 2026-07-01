using Statevia.Actions.Abstractions.Catalog;
using Statevia.Service.Api.Application.Actions.Visibility;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="DefaultActionVisibilityResolver"/> の単体テスト。</summary>
public sealed class DefaultActionVisibilityResolverTests
{
    private const string TenantA = "11111111-1111-1111-1111-111111111111";
    private const string TenantB = "22222222-2222-2222-2222-222222222222";

    /// <summary>Builtin Action は全テナントで利用可。</summary>
    [Fact]
    public void CanUse_Builtin_ReturnsTrue()
    {
        // Arrange
        var sut = new DefaultActionVisibilityResolver();
        var descriptor = new ActionDescriptor
        {
            ActionId = "statevia.action.builtin.noop",
            ModuleId = "statevia.builtin",
            Version = "1.0.0",
            Visibility = ActionVisibility.Builtin,
        };

        // Act / Assert
        Assert.True(sut.CanUse(TenantA, descriptor));
    }

    /// <summary>他テナント所有 Action は拒否する。</summary>
    [Fact]
    public void CanUse_OtherTenantAction_ReturnsFalse()
    {
        // Arrange
        var sut = new DefaultActionVisibilityResolver();
        var descriptor = new ActionDescriptor
        {
            ActionId = "com.vendor.action",
            ModuleId = "com.vendor",
            Version = "1.0.0",
            Visibility = ActionVisibility.Tenant,
            OwnerTenantId = TenantB,
        };

        // Act / Assert
        Assert.False(sut.CanUse(TenantA, descriptor));
    }

    /// <summary>所有者テナント一致時は利用可。</summary>
    [Fact]
    public void CanUse_OwnerTenant_ReturnsTrue()
    {
        // Arrange
        var sut = new DefaultActionVisibilityResolver();
        var descriptor = new ActionDescriptor
        {
            ActionId = "com.vendor.action",
            ModuleId = "com.vendor",
            Version = "1.0.0",
            Visibility = ActionVisibility.Tenant,
            OwnerTenantId = TenantA,
        };

        // Act / Assert
        Assert.True(sut.CanUse(TenantA, descriptor));
    }
}
