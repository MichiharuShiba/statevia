using Statevia.Core.Api.Application.Actions.Modules;
using Statevia.Modules;

namespace Statevia.Core.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="MaterializingModuleSourceBase"/> の射影・entry 解決共有の単体テスト。</summary>
public sealed class MaterializingModuleSourceBaseTests
{
    /// <summary>materialize 済み正本を DiscoveredModule DTO へ射影し、SourceLabel を優先して引き継ぐ。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenSourceLabelPresent_ProjectsWithSourceLabel()
    {
        // Arrange
        var sut = new TestMaterializingSource(
            priority: 1,
            sourceType: "oci",
            new MaterializedModule
            {
                ModuleDirectory = "/modules/sample",
                EntryAssemblyPath = "/modules/sample/sample.dll",
                SourceLabel = "oci:registry/sample:1.0.0",
            });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("sample", module.ModuleDirectoryName);
        Assert.Equal("/modules/sample/sample.dll", module.EntryAssemblyPath);
        Assert.Equal("oci:registry/sample:1.0.0", module.SourceLabel);
    }

    /// <summary>SourceLabel 未指定時は SourceType を既定ラベルとして用いる。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenSourceLabelMissing_FallsBackToSourceType()
    {
        // Arrange
        var sut = new TestMaterializingSource(
            priority: 1,
            sourceType: "oci",
            new MaterializedModule
            {
                ModuleDirectory = "/modules/sample",
                EntryAssemblyPath = "/modules/sample/sample.dll",
            });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Equal("oci", Assert.Single(discovered).SourceLabel);
    }

    /// <summary>末尾区切り文字付きの ModuleDirectory からも末尾ディレクトリ名を解決する。</summary>
    [Fact]
    public void ToDiscoveredModule_WhenDirectoryHasTrailingSeparator_ResolvesDirectoryName()
    {
        // Arrange
        var sut = new TestMaterializingSource(priority: 1, sourceType: "oci");
        var materialized = new MaterializedModule
        {
            ModuleDirectory = $"/modules/sample{Path.DirectorySeparatorChar}",
            EntryAssemblyPath = "/modules/sample/sample.dll",
        };

        // Act
        var module = sut.Project(materialized);

        // Assert
        Assert.Equal("sample", module.ModuleDirectoryName);
    }

    /// <summary>null の正本を射影しようとすると ArgumentNullException を投げる。</summary>
    [Fact]
    public void ToDiscoveredModule_WhenNull_Throws()
    {
        // Arrange
        var sut = new TestMaterializingSource(priority: 1, sourceType: "oci");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => sut.Project(null!));
    }

    /// <summary>entry DLL 解決は FilesystemModuleSource と同一規約を共有する。</summary>
    [Fact]
    public void TryResolveEntryAssemblyPath_WhenSingleDll_DelegatesToFilesystemResolution()
    {
        // Arrange
        var moduleDirectory = Path.Combine(Path.GetTempPath(), "statevia-modules-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(moduleDirectory);
        var dllPath = Path.Combine(moduleDirectory, "only.dll");
        File.WriteAllText(dllPath, "placeholder");

        // Act
        var ok = TestMaterializingSource.Resolve(moduleDirectory, "other-name", out var resolved, out var reason);

        // Assert
        Assert.True(ok);
        Assert.Equal(Path.GetFullPath(dllPath), resolved);
        Assert.Empty(reason);
    }

    private sealed class TestMaterializingSource(
        int priority,
        string sourceType,
        params MaterializedModule[] materialized) : MaterializingModuleSourceBase
    {
        public override int Priority { get; } = priority;

        protected override string SourceType { get; } = sourceType;

        public DiscoveredModule Project(MaterializedModule module) => ToDiscoveredModule(module);

        public static bool Resolve(string directory, string name, out string entry, out string reason) =>
            TryResolveEntryAssemblyPath(directory, name, out entry, out reason);

        protected override Task<IReadOnlyList<MaterializedModule>> MaterializeModulesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MaterializedModule>>(materialized);
    }
}
