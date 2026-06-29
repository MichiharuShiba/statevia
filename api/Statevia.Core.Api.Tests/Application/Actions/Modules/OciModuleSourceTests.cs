using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Core.Api.Application.Actions.Catalog;
using Statevia.Core.Api.Application.Actions.Modules;

namespace Statevia.Core.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="OciModuleSource"/> の materialize・射影・隔離、および取得→load 結合の単体テスト。</summary>
public sealed class OciModuleSourceTests
{
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";

    /// <summary>設定された artifact を materialize し、oci ラベル付きで discover する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenArtifactConfigured_MaterializesAndProjects()
    {
        // Arrange
        var fetcher = new FakeOciArtifactFetcher();
        fetcher.SetModule("ghcr.io", "myorg/test.module", "1.0.0", BuildModuleZip("test.module"), "sha256:abc123");
        var sut = CreateSut(fetcher, new OciModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new OciModuleArtifactOptions
                {
                    Registry = "ghcr.io",
                    Repository = "myorg/test.module",
                    Reference = "1.0.0",
                },
            ],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("test.module", module.ModuleDirectoryName);
        Assert.True(File.Exists(module.EntryAssemblyPath));
        Assert.Equal("oci:ghcr.io/myorg/test.module:1.0.0", module.SourceLabel);
    }

    /// <summary>artifact 未設定なら取得を行わず空を返す。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenNoArtifacts_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut(new FakeOciArtifactFetcher(), new OciModuleSourceOptions { Enabled = true });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>registry / repository / reference が欠落した artifact は取得せず skip する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenConfigIncomplete_SkipsArtifact()
    {
        // Arrange
        var fetcher = new FakeOciArtifactFetcher();
        var sut = CreateSut(fetcher, new OciModuleSourceOptions
        {
            Enabled = true,
            Artifacts = [new OciModuleArtifactOptions { Registry = "ghcr.io", Reference = "1.0.0" }],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
        Assert.Equal(0, fetcher.FetchCount);
    }

    /// <summary>1 artifact の取得失敗は他 artifact の materialize を妨げない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenOneArtifactFails_IsolatesFailure()
    {
        // Arrange
        var fetcher = new FakeOciArtifactFetcher();
        fetcher.SetFailure("ghcr.io", "myorg/broken", "1.0.0");
        fetcher.SetModule("ghcr.io", "myorg/test.module", "1.0.0", BuildModuleZip("test.module"), "sha256:def456");
        var sut = CreateSut(fetcher, new OciModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new OciModuleArtifactOptions { Registry = "ghcr.io", Repository = "myorg/broken", Reference = "1.0.0" },
                new OciModuleArtifactOptions { Registry = "ghcr.io", Repository = "myorg/test.module", Reference = "1.0.0" },
            ],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("test.module", module.ModuleDirectoryName);
    }

    /// <summary>Priority は設定値を返す。</summary>
    [Fact]
    public void Priority_ReturnsConfiguredValue()
    {
        // Arrange
        var sut = CreateSut(new FakeOciArtifactFetcher(), new OciModuleSourceOptions { Enabled = true, Priority = 42 });

        // Act / Assert
        Assert.Equal(42, sut.Priority);
    }

    /// <summary>OCI 取得 → materialize → ModuleHost load → Catalog 登録までが通る。</summary>
    [Fact]
    public async Task DiscoverAsync_ThenModuleHostLoad_RegistersActions()
    {
        // Arrange
        var fetcher = new FakeOciArtifactFetcher();
        fetcher.SetModule("ghcr.io", "myorg/test.module", "1.0.0", BuildModuleZip("test.module"), "sha256:feed01");
        var sut = CreateSut(fetcher, new OciModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new OciModuleArtifactOptions { Registry = "ghcr.io", Repository = "myorg/test.module", Reference = "1.0.0" },
            ],
        });
        var catalog = new InMemoryActionCatalog();
        var host = CreateModuleHost(catalog);

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);
        foreach (var module in discovered)
        {
            host.LoadDiscoveredModule(module, OwnerTenantId);
        }

        // Assert
        Assert.True(catalog.Exists("test.module.echo"));
    }

    private static OciModuleSource CreateSut(IOciArtifactFetcher fetcher, OciModuleSourceOptions options)
    {
        options.CacheRoot ??= CreateTempDirectory();
        return new OciModuleSource(
            Options.Create(options),
            fetcher,
            new StubHostEnvironment(CreateTempDirectory()),
            NullLogger<OciModuleSource>.Instance);
    }

    private static ModuleHost CreateModuleHost(InMemoryActionCatalog catalog)
    {
        var verifier = new ModuleSignatureVerifier(
            Options.Create(new ModuleSigningOptions()),
            NullLogger<ModuleSignatureVerifier>.Instance);
        var provider = new ServiceCollection().BuildServiceProvider();
        return new ModuleHost(
            new EmptyModuleSource(),
            catalog,
            new ModuleLoadCatalog(),
            verifier,
            provider,
            Options.Create(new ModuleHostOptions()),
            NullLogger<ModuleHost>.Instance);
    }

    private static byte[] BuildModuleZip(string moduleDirectoryName)
    {
        var staging = CreateTempDirectory();
        var moduleDirectory = Path.Combine(staging, moduleDirectoryName);
        Directory.CreateDirectory(moduleDirectory);

        var builtAssemblyPath = Path.Combine(AppContext.BaseDirectory, "TestActionModule.dll");
        File.Copy(builtAssemblyPath, Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll"), overwrite: true);

        var zipPath = Path.Combine(CreateTempDirectory(), $"{moduleDirectoryName}.zip");
        ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return File.ReadAllBytes(zipPath);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "statevia-oci-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeOciArtifactFetcher : IOciArtifactFetcher
    {
        private readonly Dictionary<string, OciFetchedModule> _modules = new(StringComparer.Ordinal);
        private readonly HashSet<string> _failures = new(StringComparer.Ordinal);

        public int FetchCount { get; private set; }

        public void SetModule(string registry, string repository, string reference, byte[] layerZip, string digest) =>
            _modules[Key(registry, repository, reference)] = new OciFetchedModule(layerZip, digest);

        public void SetFailure(string registry, string repository, string reference) =>
            _failures.Add(Key(registry, repository, reference));

        public Task<OciFetchedModule> FetchModuleAsync(OciModuleReference reference, CancellationToken cancellationToken)
        {
            FetchCount++;
            var key = Key(reference.Registry, reference.Repository, reference.Reference);
            if (_failures.Contains(key))
            {
                throw new InvalidOperationException($"Simulated fetch failure for '{reference.Label}'.");
            }

            return Task.FromResult(_modules[key]);
        }

        private static string Key(string registry, string repository, string reference) =>
            $"{registry}/{repository}:{reference}";
    }

    private sealed class EmptyModuleSource : IModuleSource
    {
        public int Priority => 0;

        public Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiscoveredModule>>(Array.Empty<DiscoveredModule>());
    }

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Statevia.Core.Api.Tests";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
