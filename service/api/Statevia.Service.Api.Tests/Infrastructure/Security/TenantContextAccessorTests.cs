using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="TenantContextAccessor"/> と <see cref="TenantExecutionScope"/> のスコープ挙動。</summary>
public sealed class TenantContextAccessorTests
{
    /// <summary>スコープ終了後に文脈が復元される。</summary>
    [Fact]
    public void SetContext_RestoresPreviousState_AfterDispose()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        var state = TestTenantIds.DefaultContext with { PrincipalId = Guid.NewGuid() };

        // Act
        using (accessor.SetContext(state))
        {
            // Assert
            Assert.True(accessor.IsResolved);
            Assert.Equal(state.TenantKey, accessor.TenantKey);
            Assert.Equal(state.PrincipalId, accessor.PrincipalId);
        }

        Assert.False(accessor.IsResolved);
    }

    /// <summary>RunAsync は指定文脈で action を実行する。</summary>
    [Fact]
    public async Task RunAsync_SetsTenantContext_ForDurationOfAction()
    {
        // Arrange
        var accessor = new TenantContextAccessor();
        var principalId = Guid.NewGuid();
        var state = TestTenantIds.DefaultContext with { PrincipalId = principalId };
        var observedPrincipalId = Guid.Empty;

        // Act
        await TenantExecutionScope.RunAsync(accessor, state, async () =>
        {
            observedPrincipalId = accessor.PrincipalId ?? Guid.Empty;
            await Task.CompletedTask;
        });

        // Assert
        Assert.Equal(principalId, observedPrincipalId);
        Assert.False(accessor.IsResolved);
    }

    /// <summary>RunAsync は action の戻り値を返す。</summary>
    [Fact]
    public async Task RunAsync_WithResult_ReturnsValue()
    {
        // Arrange
        var accessor = new TenantContextAccessor();

        // Act
        var value = await TenantExecutionScope.RunAsync(
            accessor,
            TestTenantIds.DefaultContext,
            () => Task.FromResult(42));

        // Assert
        Assert.Equal(42, value);
    }
}
