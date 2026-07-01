using Statevia.Service.Api.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary><see cref="ApiKeyScopeEvaluator"/> の検証。</summary>
public sealed class ApiKeyScopeEvaluatorTests
{
    /// <summary>有効スコープは展開許可と allowed scopes の交差のみ。</summary>
    [Fact]
    public void IntersectEffectiveScopes_ReturnsIntersectionOnly()
    {
        // Arrange
        var expandedPermissions = new[] { "definitions.read", "executions.write" };
        var allowedScopes = new[] { "executions.write", "executions.read" };

        // Act
        var effective = ApiKeyScopeEvaluator.IntersectEffectiveScopes(expandedPermissions, allowedScopes);

        // Assert
        Assert.Single(effective);
        Assert.Contains("executions.write", effective);
    }

    /// <summary>空入力は空集合を返す。</summary>
    [Fact]
    public void IntersectEffectiveScopes_EmptyInputs_ReturnsEmpty()
    {
        // Arrange
        var expandedPermissions = Array.Empty<string>();
        var allowedScopes = Array.Empty<string>();

        // Act
        var effective = ApiKeyScopeEvaluator.IntersectEffectiveScopes(expandedPermissions, allowedScopes);

        // Assert
        Assert.Empty(effective);
    }

    /// <summary>空白スコープは無視し trim して交差する。</summary>
    [Fact]
    public void IntersectEffectiveScopes_TrimsAndIgnoresBlankEntries()
    {
        // Arrange
        var expandedPermissions = new[] { "  executions.write  ", "" };
        var allowedScopes = new[] { "executions.write", "   " };

        // Act
        var effective = ApiKeyScopeEvaluator.IntersectEffectiveScopes(expandedPermissions, allowedScopes);

        // Assert
        Assert.Single(effective);
        Assert.Contains("executions.write", effective);
    }
}
