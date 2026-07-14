using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Infrastructure.Modules;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary><see cref="GitModuleSource"/> の materialize・射影・隔離、および取得→load 結合の単体テスト。</summary>
public sealed class GitModuleSourceTests
{
    private const string OwnerTenantId = "00000000-0000-4000-8000-000000000001";
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>archive 内 .zip を materialize し、git ラベル付きで discover する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenZipModulePath_MaterializesAndProjects()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        fetcher.SetArchive(
            "github.com",
            "acme",
            "modules",
            "main",
            CommitSha,
            BuildRepoArchiveWithModuleZip("acme-modules-aaaaaaa", "dist/test.module.zip", "test.module"));
        var sut = CreateSut(fetcher, CreateOptions("dist/test.module.zip"));

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("test.module", module.ModuleDirectoryName);
        Assert.True(File.Exists(module.EntryAssemblyPath));
        Assert.Equal("git:github.com/acme/modules@main:dist/test.module.zip", module.SourceLabel);
    }

    /// <summary>archive 内ディレクトリを materialize する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenDirectoryModulePath_Materializes()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        fetcher.SetArchive(
            "gitlab.com",
            "acme",
            "modules",
            "v1",
            CommitSha,
            BuildRepoArchiveWithModuleDirectory("modules-aaaaaaa", "src/test.module", "test.module"));
        var options = CreateOptions("src/test.module");
        options.Artifacts[0].Host = "gitlab.com";
        options.Artifacts[0].Ref = "v1";
        options.Artifacts[0].Provider = "gitlab";
        var sut = CreateSut(fetcher, options);

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("test.module", module.ModuleDirectoryName);
        Assert.Equal("git:gitlab.com/acme/modules@v1:src/test.module", module.SourceLabel);
    }

    /// <summary>artifact 未設定なら取得を行わず空を返す。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenNoArtifacts_ReturnsEmpty()
    {
        // Arrange
        var sut = CreateSut(new FakeGitArtifactFetcher(), new GitModuleSourceOptions { Enabled = true });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
    }

    /// <summary>必須項目が欠落した artifact は取得せず skip する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenConfigIncomplete_SkipsArtifact()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        var sut = CreateSut(fetcher, new GitModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new GitModuleArtifactOptions
                {
                    Host = "github.com",
                    Owner = "acme",
                    Repository = "modules",
                },
            ],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
        Assert.Equal(0, fetcher.ResolveCount);
        Assert.Equal(0, fetcher.FetchCount);
    }

    /// <summary>1 artifact の取得失敗は他 artifact の materialize を妨げない。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenOneArtifactFails_IsolatesFailure()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        fetcher.SetFailure("github.com", "acme", "modules", "broken");
        fetcher.SetArchive(
            "github.com",
            "acme",
            "modules",
            "main",
            CommitSha,
            BuildRepoArchiveWithModuleZip("acme-modules-aaaaaaa", "dist/test.module.zip", "test.module"));
        var sut = CreateSut(fetcher, new GitModuleSourceOptions
        {
            Enabled = true,
            Artifacts =
            [
                new GitModuleArtifactOptions
                {
                    Host = "github.com",
                    Owner = "acme",
                    Repository = "modules",
                    Ref = "broken",
                    ModulePath = "dist/broken.zip",
                    Provider = "github",
                },
                new GitModuleArtifactOptions
                {
                    Host = "github.com",
                    Owner = "acme",
                    Repository = "modules",
                    Ref = "main",
                    ModulePath = "dist/test.module.zip",
                    Provider = "github",
                },
            ],
        });

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        var module = Assert.Single(discovered);
        Assert.Equal("test.module", module.ModuleDirectoryName);
    }

    /// <summary>ModulePath のパストラバーサルは materialize 失敗として隔離する。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenModulePathTraversal_SkipsArtifact()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        fetcher.SetArchive(
            "github.com",
            "acme",
            "modules",
            "main",
            CommitSha,
            BuildRepoArchiveWithModuleZip("acme-modules-aaaaaaa", "dist/test.module.zip", "test.module"));
        var sut = CreateSut(fetcher, CreateOptions("../escape.zip"));

        // Act
        var discovered = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(discovered);
        Assert.Equal(1, fetcher.ResolveCount);
        Assert.Equal(1, fetcher.FetchCount);
    }

    /// <summary>Priority は設定値を返す。</summary>
    [Fact]
    public void Priority_ReturnsConfiguredValue()
    {
        // Arrange
        var sut = CreateSut(new FakeGitArtifactFetcher(), new GitModuleSourceOptions { Enabled = true, Priority = 77 });

        // Act / Assert
        Assert.Equal(77, sut.Priority);
    }

    /// <summary>Git 取得 → materialize → ModuleHost load → Catalog 登録までが通る。</summary>
    [Fact]
    public async Task DiscoverAsync_ThenModuleHostLoad_RegistersActions()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        fetcher.SetArchive(
            "github.com",
            "acme",
            "modules",
            "main",
            CommitSha,
            BuildRepoArchiveWithModuleZip("acme-modules-aaaaaaa", "dist/test.module.zip", "test.module"));
        var sut = CreateSut(fetcher, CreateOptions("dist/test.module.zip"));
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

    /// <summary>同一 content identity がキャッシュ済みなら 2 回目の discover で archive 取得をスキップする。</summary>
    [Fact]
    public async Task DiscoverAsync_WhenIdentityCached_SkipsArchiveFetch()
    {
        // Arrange
        var fetcher = new FakeGitArtifactFetcher();
        fetcher.SetArchive(
            "github.com",
            "acme",
            "modules",
            "main",
            CommitSha,
            BuildRepoArchiveWithModuleZip("acme-modules-aaaaaaa", "dist/test.module.zip", "test.module"));
        var cacheRoot = CreateTempDirectory();
        var options = CreateOptions("dist/test.module.zip");
        options.CacheRoot = cacheRoot;
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

    /// <summary>content identity は commit SHA と ModulePath を組み合わせる。</summary>
    [Fact]
    public void BuildContentIdentity_CombinesShaAndPath()
    {
        // Act / Assert
        Assert.Equal(
            $"sha:{CommitSha}+path:dist/test.module.zip",
            GitModuleSource.BuildContentIdentity(CommitSha, "dist/test.module.zip"));
    }

    /// <summary>既知 Host のみ provider を推定し、未知 Host は明示 Provider を要求する。</summary>
    [Fact]
    public void GitModuleProviders_Resolve_InfersOnlyKnownHosts()
    {
        // Act / Assert
        Assert.Equal(GitModuleProviders.GitHub, GitModuleProviders.Resolve(null, "github.com"));
        Assert.Equal(GitModuleProviders.GitHub, GitModuleProviders.Resolve(null, "www.github.com"));
        Assert.Equal(GitModuleProviders.GitLab, GitModuleProviders.Resolve(null, "gitlab.com"));
        Assert.Equal(GitModuleProviders.GitLab, GitModuleProviders.Resolve(null, "gitlab.com:443"));
        Assert.Equal(GitModuleProviders.GitHub, GitModuleProviders.Resolve("github", "git.example.com"));
        Assert.Equal(GitModuleProviders.GitLab, GitModuleProviders.Resolve("gitlab", "git.example.com"));
        Assert.Throws<ArgumentException>(() => GitModuleProviders.Resolve(null, "git.example.com"));
        Assert.Throws<ArgumentException>(() => GitModuleProviders.Resolve("bitbucket", "bitbucket.org"));
    }

    /// <summary>40 桁 hex はフル commit SHA と判定する。</summary>
    [Fact]
    public void LooksLikeFullCommitSha_DetectsHex40()
    {
        // Act / Assert
        Assert.True(HttpGitArtifactFetcher.LooksLikeFullCommitSha(CommitSha));
        Assert.False(HttpGitArtifactFetcher.LooksLikeFullCommitSha("main"));
        Assert.False(HttpGitArtifactFetcher.LooksLikeFullCommitSha("abc"));
    }

    private static GitModuleSourceOptions CreateOptions(string modulePath) =>
        new()
        {
            Enabled = true,
            Artifacts =
            [
                new GitModuleArtifactOptions
                {
                    Host = "github.com",
                    Owner = "acme",
                    Repository = "modules",
                    Ref = "main",
                    ModulePath = modulePath,
                    Provider = "github",
                },
            ],
        };

    private static GitModuleSource CreateSut(IGitArtifactFetcher fetcher, GitModuleSourceOptions options)
    {
        options.CacheRoot ??= CreateTempDirectory();
        return new GitModuleSource(
            Options.Create(options),
            fetcher,
            new StubHostEnvironment(CreateTempDirectory()),
            NullLogger<GitModuleSource>.Instance);
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

    /// <summary>GitHub 風トップレベル配下に module zip を置く repo archive を作る。</summary>
    private static byte[] BuildRepoArchiveWithModuleZip(
        string topLevelDirectory,
        string moduleZipRelativePath,
        string moduleDirectoryName)
    {
        var staging = CreateTempDirectory();
        var top = Path.Combine(staging, topLevelDirectory);
        var moduleZipAbsolute = Path.Combine(top, moduleZipRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(moduleZipAbsolute)!);
        File.WriteAllBytes(moduleZipAbsolute, BuildModuleZip(moduleDirectoryName));

        var archivePath = Path.Combine(CreateTempDirectory(), "repo.zip");
        ZipFile.CreateFromDirectory(staging, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return File.ReadAllBytes(archivePath);
    }

    /// <summary>GitLab 風トップレベル配下に module ディレクトリを置く repo archive を作る。</summary>
    private static byte[] BuildRepoArchiveWithModuleDirectory(
        string topLevelDirectory,
        string moduleRelativePath,
        string moduleDirectoryName)
    {
        var staging = CreateTempDirectory();
        var moduleDirectory = Path.Combine(
            staging,
            topLevelDirectory,
            moduleRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(moduleDirectory);

        var builtAssemblyPath = Path.Combine(AppContext.BaseDirectory, "TestActionModule.dll");
        File.Copy(builtAssemblyPath, Path.Combine(moduleDirectory, $"{moduleDirectoryName}.dll"), overwrite: true);

        var archivePath = Path.Combine(CreateTempDirectory(), "repo.zip");
        ZipFile.CreateFromDirectory(staging, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return File.ReadAllBytes(archivePath);
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
        var path = Path.Combine(Path.GetTempPath(), "statevia-git-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeGitArtifactFetcher : IGitArtifactFetcher
    {
        private readonly Dictionary<string, (string Sha, byte[] Archive)> _archives = new(StringComparer.Ordinal);
        private readonly HashSet<string> _failures = new(StringComparer.Ordinal);

        public int FetchCount { get; private set; }

        public int ResolveCount { get; private set; }

        public void SetArchive(string host, string owner, string repository, string @ref, string sha, byte[] archive) =>
            _archives[Key(host, owner, repository, @ref)] = (sha, archive);

        public void SetFailure(string host, string owner, string repository, string @ref) =>
            _failures.Add(Key(host, owner, repository, @ref));

        public Task<string> ResolveCommitShaAsync(GitModuleReference reference, CancellationToken cancellationToken)
        {
            ResolveCount++;
            var key = Key(reference.Host, reference.Owner, reference.Repository, reference.Ref);
            if (_failures.Contains(key))
            {
                throw new InvalidOperationException($"Simulated resolve failure for '{reference.Label}'.");
            }

            return Task.FromResult(_archives[key].Sha);
        }

        public Task<byte[]> FetchArchiveAsync(
            GitModuleReference reference,
            string commitSha,
            CancellationToken cancellationToken)
        {
            FetchCount++;
            var key = Key(reference.Host, reference.Owner, reference.Repository, reference.Ref);
            if (_failures.Contains(key))
            {
                throw new InvalidOperationException($"Simulated fetch failure for '{reference.Label}'.");
            }

            return Task.FromResult(_archives[key].Archive);
        }

        private static string Key(string host, string owner, string repository, string gitRef) =>
            $"{host}/{owner}/{repository}@{gitRef}";
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
