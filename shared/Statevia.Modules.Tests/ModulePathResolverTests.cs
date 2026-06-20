namespace Statevia.Modules.Tests;

/// <summary><see cref="ModulePathResolver"/> の単体テスト。</summary>
public sealed class ModulePathResolverTests
{
    /// <summary>環境変数が最優先で解決される。</summary>
    [Fact]
    public void Resolve_WhenEnvironmentPathSet_UsesEnvironmentPath()
    {
        // Arrange
        var contentRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-content"));
        var envPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-env-modules"));

        // Act
        var resolved = ModulePathResolver.Resolve(contentRoot, envPath, configurationPath: "/config/modules");

        // Assert
        Assert.Equal(envPath, resolved);
    }

    /// <summary>環境変数が無いとき設定パスが使われる。</summary>
    [Fact]
    public void Resolve_WhenConfigurationPathSet_UsesConfigurationPath()
    {
        // Arrange
        var contentRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-content"));
        var configPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-config-modules"));

        // Act
        var resolved = ModulePathResolver.Resolve(contentRoot, environmentPath: null, configPath);

        // Assert
        Assert.Equal(configPath, resolved);
    }

    /// <summary>相対パスは content root 基準で絶対化される。</summary>
    [Fact]
    public void Resolve_WhenRelativePath_ResolvesAgainstContentRoot()
    {
        // Arrange
        var contentRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-content"));

        // Act
        var resolved = ModulePathResolver.Resolve(contentRoot, environmentPath: null, configurationPath: "custom-modules");

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "custom-modules")), resolved);
    }

    /// <summary>未設定時は content root 配下の modules が既定になる。</summary>
    [Fact]
    public void Resolve_WhenUnset_UsesDefaultModulesDirectory()
    {
        // Arrange
        var contentRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "statevia-content"));

        // Act
        var resolved = ModulePathResolver.Resolve(contentRoot, environmentPath: null, configurationPath: null);

        // Assert
        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, "modules")), resolved);
    }
}
