using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Statevia.ActionHost;
using Statevia.ActionHost.Modules;
using Statevia.Modules;

namespace Statevia.ActionHost.Tests;

/// <summary><see cref="ActionHostModuleLoader"/> の統合テスト。</summary>
public sealed class ActionHostModuleLoaderTests
{
    /// <summary>有効な module を load すると Action が登録される。</summary>
    [Fact]
    public void LoadAll_WhenValidModulePresent_RegistersActions()
    {
        // Arrange
        using var factory = new ActionHostWebApplicationFactory();
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        // StartupService が起動時に load 済み
        var loaded = registry.TryGet("test.module.echo", out var registration);

        // Assert
        Assert.True(loaded);
        Assert.NotNull(registration);
        Assert.Equal("test.module", registration.ModuleId);
    }

    /// <summary>同一 actionId の二重登録はスキップされる。</summary>
    [Fact]
    public void LoadAll_WhenDuplicateActionExists_KeepsFirstRegistration()
    {
        // Arrange
        var modulesRoot = TestModuleLayout.CopyBuiltAssembly("test.module");
        TestModuleLayout.CopyBuiltAssembly("test.module.copy", modulesRoot);
        using var factory = ActionHostWebApplicationFactory.ForModulesRoot(modulesRoot);
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        var count = registry.Count;

        // Assert
        Assert.Equal(1, count);
        Assert.True(registry.TryGet("test.module.echo", out _));
    }

    /// <summary>IActionModule を含まない DLL はスキップされる。</summary>
    [Fact]
    public void LoadAll_WhenAssemblyHasNoActionModule_SkipsModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "broken.module");
        Directory.CreateDirectory(moduleDirectory);
        var hostDll = typeof(ActionHostModuleLoader).Assembly.Location;
        File.Copy(hostDll, Path.Combine(moduleDirectory, "broken.module.dll"), overwrite: true);
        using var factory = ActionHostWebApplicationFactory.ForModulesRoot(modulesRoot);
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        var count = registry.Count;

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>不正な Action 登録はスキップされ、有効な Action のみ登録される。</summary>
    [Fact]
    public void LoadAll_WhenMalformedModulePresent_RegistersOnlyValidActions()
    {
        // Arrange
        var modulesRoot = TestModuleLayout.CopyBuiltAssembly(
            "malformed.module",
            assemblyFileName: "MalformedActionModule.dll");
        using var factory = ActionHostWebApplicationFactory.ForModulesRoot(modulesRoot);
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        var count = registry.Count;

        // Assert
        Assert.Equal(1, count);
        Assert.True(registry.TryGet("malformed.module.good", out _));
        Assert.False(registry.TryGet("wrong.prefix.action", out _));
    }

    /// <summary>公開コンストラクタが無い Module はスキップされる。</summary>
    [Fact]
    public void LoadAll_WhenModuleCannotBeInstantiated_SkipsModule()
    {
        // Arrange
        var modulesRoot = TestModuleLayout.CopyBuiltAssembly(
            "private.ctor",
            assemblyFileName: "PrivateCtorActionModule.dll");
        using var factory = ActionHostWebApplicationFactory.ForModulesRoot(modulesRoot);
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        var count = registry.Count;

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>破損 DLL は load 失敗としてスキップされる。</summary>
    [Fact]
    public void LoadAll_WhenCorruptDllPresent_SkipsModule()
    {
        // Arrange
        var modulesRoot = CreateTempDirectory();
        var moduleDirectory = Path.Combine(modulesRoot, "corrupt.module");
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, "corrupt.module.dll"), "not-a-valid-assembly");
        using var factory = ActionHostWebApplicationFactory.ForModulesRoot(modulesRoot);
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        var count = registry.Count;

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>ModulesPath 未設定時は環境変数から modules ルートを解決する。</summary>
    [Fact]
    public void LoadAll_WhenModulesPathUnset_UsesEnvironmentVariable()
    {
        // Arrange
        var modulesRoot = TestModuleLayout.CopyBuiltAssembly("test.module");
        var previous = Environment.GetEnvironmentVariable(ModulePathResolver.EnvironmentVariable);
        try
        {
            using var factory = ActionHostWebApplicationFactory.ForEnvironmentModulesRoot(modulesRoot);
            var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

            // Act
            var loaded = registry.TryGet("test.module.echo", out _);

            // Assert
            Assert.True(loaded);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModulePathResolver.EnvironmentVariable, previous);
        }
    }

    /// <summary>相対 ModulesPath は content root 基準で解決される。</summary>
    [Fact]
    public void LoadAll_WhenRelativeModulesPathConfigured_RegistersActions()
    {
        // Arrange
        var contentRoot = CreateTempDirectory();
        const string relativeModulesDirectory = "relative-modules";
        using var factory = ActionHostWebApplicationFactory.ForRelativeModulesRoot(
            contentRoot,
            relativeModulesDirectory);
        var registry = factory.Services.GetRequiredService<ActionHostActionRegistry>();

        // Act
        var loaded = registry.TryGet("test.module.echo", out _);

        // Assert
        Assert.True(loaded);
    }

    /// <summary>ListenUrl 設定は Options にバインドされる。</summary>
    [Fact]
    public void Configure_WhenListenUrlSet_ExposesOptionValue()
    {
        // Arrange
        const string listenUrl = "http://127.0.0.1:5999";
        using var factory = ActionHostWebApplicationFactory.WithListenUrl(listenUrl);
        var options = factory.Services.GetRequiredService<IOptions<ActionHostOptions>>().Value;

        // Act
        var configuredListenUrl = options.ListenUrl;

        // Assert
        Assert.Equal(listenUrl, configuredListenUrl);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-action-host-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
