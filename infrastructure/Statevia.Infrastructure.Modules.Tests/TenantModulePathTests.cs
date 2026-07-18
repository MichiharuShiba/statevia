namespace Statevia.Infrastructure.Modules.Tests;

/// <summary><see cref="TenantModulePath"/> の単体テスト。</summary>
public sealed class TenantModulePathTests
{
    /// <summary>妥当な tenant_key を受け入れる。</summary>
    [Theory]
    [InlineData("default")]
    [InlineData("acme-corp")]
    [InlineData("a")]
    [InlineData("t1.prod")]
    public void IsValidTenantKey_WhenValid_ReturnsTrue(string tenantKey)
    {
        // Act
        var valid = TenantModulePath.IsValidTenantKey(tenantKey);

        // Assert
        Assert.True(valid);
    }

    /// <summary>不正な tenant_key を拒否する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("../evil")]
    [InlineData("ACME")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("has/slash")]
    public void IsValidTenantKey_WhenInvalid_ReturnsFalse(string? tenantKey)
    {
        // Act
        var valid = TenantModulePath.IsValidTenantKey(tenantKey);

        // Assert
        Assert.False(valid);
    }

    /// <summary>テナント配下パスを modules ルート配下に解決する。</summary>
    [Fact]
    public void ResolveTenantModulesRoot_WhenValid_ReturnsTenantSubdirectory()
    {
        // Arrange
        var modulesRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-modules-root"));

        // Act
        var resolved = TenantModulePath.ResolveTenantModulesRoot(modulesRoot, "acme-corp");

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(modulesRoot, "acme-corp")), resolved);
    }

    /// <summary>パストラバーサル相当のキーは拒否する。</summary>
    [Fact]
    public void ResolveTenantModulesRoot_WhenTraversalKey_Throws()
    {
        // Arrange
        var modulesRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-modules-root"));

        // Act
        var exception = Record.Exception(() =>
            TenantModulePath.ResolveTenantModulesRoot(modulesRoot, ".."));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    /// <summary>空の tenant_key は拒否する。</summary>
    [Fact]
    public void NormalizeTenantKey_WhenEmpty_Throws()
    {
        // Act
        var exception = Record.Exception(() => TenantModulePath.NormalizeTenantKey("  "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }
}
