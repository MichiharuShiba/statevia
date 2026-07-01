using Microsoft.Extensions.Options;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="TenantExecutionPolicyProvider"/> の単体テスト。</summary>
public sealed class TenantExecutionPolicyProviderTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    /// <summary>設定済みテナントは Tenant スコープの下限ポリシーを返す。</summary>
    [Fact]
    public void GetPolicies_WhenTenantConfigured_ReturnsTenantScopedPolicy()
    {
        // Arrange
        var sut = CreateSut(new Dictionary<string, ScopedExecutionPolicyOptions>
        {
            [TenantId] = new() { MinimumMode = ActionExecutionMode.Container },
        });
        var context = new ActionExecutionContext(TenantId, "Production", null);

        // Act
        var policies = sut.GetPolicies(context);

        // Assert
        var policy = Assert.Single(policies);
        Assert.Equal(ExecutionPolicyScope.Tenant, policy.Scope);
        Assert.Equal(ActionExecutionMode.Container, policy.Policy.MinimumMode);
    }

    /// <summary>未設定テナントは空を返す。</summary>
    [Fact]
    public void GetPolicies_WhenTenantNotConfigured_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut(new Dictionary<string, ScopedExecutionPolicyOptions>
        {
            ["other-tenant"] = new() { MinimumMode = ActionExecutionMode.Container },
        });
        var context = new ActionExecutionContext(TenantId, "Production", null);

        // Act
        var policies = sut.GetPolicies(context);

        // Assert
        Assert.Empty(policies);
    }

    /// <summary>MinimumMode 未指定のテナント設定は空を返す。</summary>
    [Fact]
    public void GetPolicies_WhenMinimumModeMissing_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut(new Dictionary<string, ScopedExecutionPolicyOptions>
        {
            [TenantId] = new() { MinimumMode = null },
        });
        var context = new ActionExecutionContext(TenantId, "Production", null);

        // Act
        var policies = sut.GetPolicies(context);

        // Assert
        Assert.Empty(policies);
    }

    private static TenantExecutionPolicyProvider CreateSut(
        Dictionary<string, ScopedExecutionPolicyOptions> tenants) =>
        new(Options.Create(new ExecutionPolicyOptions { Tenants = tenants }));
}
