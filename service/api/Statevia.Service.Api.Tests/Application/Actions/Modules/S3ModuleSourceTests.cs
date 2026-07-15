using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="S3ModuleSource"/> の materialize・射影・隔離、および取得→load 結合の単体テスト。</summary>
public sealed class S3ModuleSourceTests
{
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";

    /// <summary>設定された artifact を materialize し、s3 ラベル付きで discover する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenArtifactConfigured_MaterializesAndProjects()
    {
        // Arrange
        var fetcher = new FakeS3ArtifactFetcher();
        fetcher.SetModule("my-bucket", "modules/test.module.zip", BuildModuleZip("test.module"), "etag:abc123");
        var sut = CreateSut(fetcher, new S3ModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new S3ModuleArtifactOptions
                {
                    Bucket = "my-bucket",
                    Key = "modules/test.module.zip",
                    Region = "ap-northeast-1",
                },
            ],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("test.module", module.ModuleDirectoryName);
        Assert.True(File.Exists(module.EntryAssemblyPath));
        Assert.Equal("s3:my-bucket/modules/test.module.zip", module.SourceLabel);
    }

    /// <summary>artifact 未設定なら取得を行わず空を返す。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenNoArtifacts_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut(new FakeS3ArtifactFetcher(), new S3ModuleSourceOptions { Enabled = true });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>bucket / key が欠落した artifact は取得せず skip する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenConfigIncomplete_SkipsArtifact()
    {
        // Arrange
        var fetcher = new FakeS3ArtifactFetcher();
        var sut = CreateSut(fetcher, new S3ModuleSourceOptions
        {
            Enabled = true,
            Artifacts = [new S3ModuleArtifactOptions { Bucket = "my-bucket" }],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
        Assert.Equal(0, fetcher.FetchCount);
        Assert.Equal(0, fetcher.ResolveCount);
    }

    /// <summary>1 artifact の取得失敗は他 artifact の materialize を妨げない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenOneArtifactFails_IsolatesFailure()
    {
        // Arrange
        var fetcher = new FakeS3ArtifactFetcher();
        fetcher.SetFailure("my-bucket", "modules/broken.zip");
        fetcher.SetModule("my-bucket", "modules/test.module.zip", BuildModuleZip("test.module"), "etag:def456");
        var sut = CreateSut(fetcher, new S3ModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new S3ModuleArtifactOptions { Bucket = "my-bucket", Key = "modules/broken.zip", Region = "ap-northeast-1" },
                new S3ModuleArtifactOptions { Bucket = "my-bucket", Key = "modules/test.module.zip", Region = "ap-northeast-1" },
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
        var sut = CreateSut(new FakeS3ArtifactFetcher(), new S3ModuleSourceOptions { Enabled = true, Priority = 42 });

        // Act / Assert
        Assert.Equal(42, sut.Priority);
    }

    /// <summary>S3 取得 → materialize → ModuleHost load → Catalog 登録までが通る。</summary>
    [Fact]
    public async Task DiscoverAsync_ThenModuleHostLoad_RegistersActions()
    {
        // Arrange
        var fetcher = new FakeS3ArtifactFetcher();
        fetcher.SetModule("my-bucket", "modules/test.module.zip", BuildModuleZip("test.module"), "etag:feed01");
        var sut = CreateSut(fetcher, new S3ModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new S3ModuleArtifactOptions { Bucket = "my-bucket", Key = "modules/test.module.zip", Region = "ap-northeast-1" },
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

    /// <summary>同一 content identity がキャッシュ済みなら 2 回目の discover で blob 取得をスキップする。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenIdentityCached_SkipsBlobFetch()
    {
        // Arrange
        var fetcher = new FakeS3ArtifactFetcher();
        fetcher.SetModule("my-bucket", "modules/test.module.zip", BuildModuleZip("test.module"), "etag:cache01");
        var cacheRoot = CreateTempDirectory();
        var options = new S3ModuleSourceOptions
        {
            Enabled = true,
            CacheRoot = cacheRoot,
            Artifacts =
            [
                new S3ModuleArtifactOptions { Bucket = "my-bucket", Key = "modules/test.module.zip", Region = "ap-northeast-1" },
            ],
        };
        var sut = CreateSut(fetcher, options);

        // Act
        var first = await sut.DiscoverAsync(CancellationToken.None);
        var second = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(2, fetcher.ResolveCount);
        Assert.Equal(1, fetcher.FetchCount);
        Assert.Equal(first[0].EntryAssemblyPath, second[0].EntryAssemblyPath);
    }

    /// <summary>ETag と VersionId から content identity を組み立てる。</summary>
    [Fact]
    public void BuildContentIdentity_CombinesVersionAndEtag()
    {
        // Act / Assert
        Assert.Equal("etag:abc", AwsS3ArtifactFetcher.BuildContentIdentity("\"abc\"", null));
        Assert.Equal("ver:v1+etag:abc", AwsS3ArtifactFetcher.BuildContentIdentity("\"abc\"", "v1"));
        Assert.Equal("ver:v1", AwsS3ArtifactFetcher.BuildContentIdentity(null, "v1"));
    }

    private static S3ModuleSource CreateSut(IS3ArtifactFetcher fetcher, S3ModuleSourceOptions options)
    {
        options.CacheRoot ??= CreateTempDirectory();
        return new S3ModuleSource(
            Options.Create(options),
            fetcher,
            new StubHostEnvironment(CreateTempDirectory()),
            NullLogger<S3ModuleSource>.Instance);
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
        var path = Path.Combine(Path.GetTempPath(), "statevia-s3-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeS3ArtifactFetcher : IS3ArtifactFetcher
    {
        private readonly Dictionary<string, S3FetchedModule> _modules = new(StringComparer.Ordinal);
        private readonly HashSet<string> _failures = new(StringComparer.Ordinal);

        public int FetchCount { get; private set; }

        public int ResolveCount { get; private set; }

        public void SetModule(string bucket, string key, byte[] zipBytes, string identity) =>
            _modules[Key(bucket, key)] = new S3FetchedModule(zipBytes, identity);

        public void SetFailure(string bucket, string key) =>
            _failures.Add(Key(bucket, key));

        public Task<string> ResolveContentIdentityAsync(S3ModuleReference reference, CancellationToken cancellationToken)
        {
            ResolveCount++;
            var key = Key(reference.Bucket, reference.Key);
            if (_failures.Contains(key))
            {
                throw new InvalidOperationException($"Simulated resolve failure for '{reference.Label}'.");
            }

            return Task.FromResult(_modules[key].ContentIdentity);
        }

        public Task<S3FetchedModule> FetchModuleAsync(S3ModuleReference reference, CancellationToken cancellationToken)
        {
            FetchCount++;
            var key = Key(reference.Bucket, reference.Key);
            if (_failures.Contains(key))
            {
                throw new InvalidOperationException($"Simulated fetch failure for '{reference.Label}'.");
            }

            return Task.FromResult(_modules[key]);
        }

        private static string Key(string bucket, string objectKey) => $"{bucket}/{objectKey}";
    }

    private sealed class EmptyModuleSource : IModuleSource
    {
        public int Priority => 0;

        public Task<IReadOnlyList<DiscoveredModule>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiscoveredModule>>(Array.Empty<DiscoveredModule>());
    }

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
