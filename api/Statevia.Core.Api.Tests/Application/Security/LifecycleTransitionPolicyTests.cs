using Statevia.Core.Api.Application.Security;

namespace Statevia.Core.Api.Tests.Application.Security;

/// <summary><see cref="LifecycleTransitionPolicy"/> の検証。</summary>
public sealed class LifecycleTransitionPolicyTests
{
    /// <summary>Archived から Active への復帰は禁止。</summary>
    [Fact]
    public void CanTransition_ArchivedToActive_ReturnsFalse()
    {
        // Act
        var allowed = LifecycleTransitionPolicy.CanTransition(TenantLifecycle.Archived, TenantLifecycle.Active);

        // Assert
        Assert.False(allowed);
    }

    /// <summary>Suspended から Active への復帰は許可。</summary>
    [Fact]
    public void CanTransition_SuspendedToActive_ReturnsTrue()
    {
        // Act
        var allowed = LifecycleTransitionPolicy.CanTransition(TenantLifecycle.Suspended, TenantLifecycle.Active);

        // Assert
        Assert.True(allowed);
    }

    /// <summary>同一状態への遷移は常に許可。</summary>
    [Fact]
    public void CanTransition_SameState_ReturnsTrue()
    {
        // Act
        var allowed = LifecycleTransitionPolicy.CanTransition(TenantLifecycle.Active, TenantLifecycle.Active);

        // Assert
        Assert.True(allowed);
    }

    /// <summary>Active から Suspended への遷移は許可。</summary>
    [Fact]
    public void CanTransition_ActiveToSuspended_ReturnsTrue()
    {
        // Act
        var allowed = LifecycleTransitionPolicy.CanTransition(TenantLifecycle.Active, TenantLifecycle.Suspended);

        // Assert
        Assert.True(allowed);
    }

    /// <summary>禁止遷移は ArgumentException を送出する。</summary>
    [Fact]
    public void EnsureCanTransition_DisallowedTransition_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            LifecycleTransitionPolicy.EnsureCanTransition(TenantLifecycle.Archived, TenantLifecycle.Active));

        Assert.Contains("not allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>初期状態は Active。</summary>
    [Fact]
    public void InitialState_IsActive()
    {
        // Assert
        Assert.Equal(TenantLifecycle.Active, LifecycleTransitionPolicy.InitialState);
    }
}
