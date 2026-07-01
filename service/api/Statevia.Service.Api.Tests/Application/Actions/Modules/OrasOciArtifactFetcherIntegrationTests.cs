using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using OrasProject.Oras;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using Statevia.Service.Api.Application.Actions.Modules;

namespace Statevia.Service.Api.Tests.Application.Actions.Modules;

/// <summary>
/// 実 OCI レジストリ（<c>registry:2</c>）に対する <see cref="OrasOciArtifactFetcher"/> の結合テスト。
/// </summary>
/// <remarks>
/// <para>
/// ORAS .NET でローカルレジストリへ artifact を push し、fetcher が「manifest 取得 → レイヤ選択 → blob 取得」を
/// 正しく行うことを検証する。レイヤ選択規則（専用 media type 優先・無ければ単一レイヤ）を実 registry 経由で固定する。
/// </para>
/// <para>
/// Docker 未起動時は <see cref="OciRegistryFixture.SkipReason"/> により全テストを Skip する（Failed にしない）。
/// </para>
/// </remarks>
public sealed class OrasOciArtifactFetcherIntegrationTests(OciRegistryFixture fixture)
    : IClassFixture<OciRegistryFixture>
{
    /// <summary>テスト artifact の artifactType（manifest media type は OCI image manifest のまま）。</summary>
    private const string ModuleArtifactType = "application/vnd.statevia.module.artifact.v1";

    /// <summary>専用 media type を含まない単一レイヤは、その唯一のレイヤが取得される。</summary>
    [SkippableFact]
    public async Task FetchModuleAsync_WhenSingleNonModuleLayer_ReturnsThatLayer()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason ?? "Docker unavailable");

        // Arrange
        var registry = fixture.RegistryEndpoint;
        const string repository = "statevia/single-layer-module";
        const string tag = "1.0.0";
        var layerBytes = Encoding.UTF8.GetBytes("single-layer-module-zip-payload");
        await PushArtifactAsync(
            registry,
            repository,
            tag,
            [(MediaType.ImageLayer, layerBytes)],
            CancellationToken.None);

        using var sut = new OrasOciArtifactFetcher(
            new SingleHttpClientFactory(),
            NullLogger<OrasOciArtifactFetcher>.Instance);
        var reference = new OciModuleReference(registry, repository, tag, null, null, null, PlainHttp: true);

        // Act
        var fetched = await sut.FetchModuleAsync(reference, CancellationToken.None);

        // Assert
        Assert.Equal(layerBytes, fetched.LayerZip);
        Assert.StartsWith("sha256:", fetched.ManifestDigest);
    }

    /// <summary>専用 media type を含む複数レイヤは、media type 一致レイヤが選択される（デコイは無視）。</summary>
    [SkippableFact]
    public async Task FetchModuleAsync_WhenMultipleLayersWithModuleMediaType_SelectsModuleLayer()
    {
        Skip.If(fixture.SkipReason is not null, fixture.SkipReason ?? "Docker unavailable");

        // Arrange
        var registry = fixture.RegistryEndpoint;
        const string repository = "statevia/multi-layer-module";
        const string tag = "2.1.0";
        var decoyBytes = Encoding.UTF8.GetBytes("decoy-non-module-layer");
        var moduleBytes = Encoding.UTF8.GetBytes("real-statevia-module-zip-payload");
        await PushArtifactAsync(
            registry,
            repository,
            tag,
            [
                (MediaType.ImageLayer, decoyBytes),
                (OrasOciArtifactFetcher.ModuleLayerMediaType, moduleBytes),
            ],
            CancellationToken.None);

        using var sut = new OrasOciArtifactFetcher(
            new SingleHttpClientFactory(),
            NullLogger<OrasOciArtifactFetcher>.Instance);
        var reference = new OciModuleReference(registry, repository, tag, null, null, null, PlainHttp: true);

        // Act
        var fetched = await sut.FetchModuleAsync(reference, CancellationToken.None);

        // Assert
        Assert.Equal(moduleBytes, fetched.LayerZip);
        Assert.NotEqual(decoyBytes, fetched.LayerZip);
    }

    /// <summary>
    /// 指定レイヤ群を blob として push し、それらを参照する OCI image manifest を pack して tag を付与する。
    /// </summary>
    /// <remarks>レイヤ blob は manifest より先に存在している必要があるため、push → pack → tag の順で行う。</remarks>
    private static async Task PushArtifactAsync(
        string registry,
        string repository,
        string tag,
        IReadOnlyList<(string MediaType, byte[] Data)> layers,
        CancellationToken cancellationToken)
    {
        var pushRepository = new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse($"{registry}/{repository}"),
            Client = new PlainClient(SharedPushHttpClient),
            PlainHttp = true,
        });

        var layerDescriptors = new List<Descriptor>(layers.Count);
        // レイヤ push は順序付き I/O のため foreach を採用する。
        foreach (var (mediaType, data) in layers)
        {
            var descriptor = Descriptor.Create(data, mediaType);
            await pushRepository.PushAsync(descriptor, new MemoryStream(data), cancellationToken);
            layerDescriptors.Add(descriptor);
        }

        var packOptions = new PackManifestOptions { Layers = layerDescriptors };
        var manifestDescriptor = await Packer.PackManifestAsync(
            pushRepository,
            Packer.ManifestVersion.Version1_1,
            ModuleArtifactType,
            packOptions,
            cancellationToken);
        await pushRepository.TagAsync(manifestDescriptor, tag, cancellationToken);
    }

    /// <summary>push に用いる共有 <see cref="HttpClient"/>（plain HTTP・テスト用）。</summary>
    private static readonly HttpClient SharedPushHttpClient = new();

    /// <summary>fetcher が要求する named client を単一の <see cref="HttpClient"/> で満たすテスト用ファクトリ。</summary>
    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private static readonly HttpClient Client = new();

        public HttpClient CreateClient(string name) => Client;
    }
}

/// <summary>
/// テスト用にローカル OCI レジストリ（<c>registry:2</c>）を 1 つ起動・破棄する xunit フィクスチャ。
/// </summary>
/// <remarks>
/// Docker が利用不可（未起動・未インストール）の場合は起動例外を捕捉し、<see cref="SkipReason"/> に理由を保持する。
/// 各テストは <see cref="SkipReason"/> が非 null のとき Skip する。
/// </remarks>
public sealed class OciRegistryFixture : IAsyncLifetime
{
    private const ushort RegistryContainerPort = 5000;

    private IContainer? _container;

    /// <summary>fetcher / push に渡すレジストリエンドポイント（<c>host:port</c>）。Skip 時は空文字。</summary>
    public string RegistryEndpoint { get; private set; } = string.Empty;

    /// <summary>Docker 利用不可時の Skip 理由。利用可能なら null。</summary>
    public string? SkipReason { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            _container = new ContainerBuilder("registry:2")
                .WithPortBinding(RegistryContainerPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(RegistryContainerPort).ForPath("/v2/")))
                .Build();
            await _container.StartAsync();
            RegistryEndpoint = $"{_container.Hostname}:{_container.GetMappedPublicPort(RegistryContainerPort)}";
        }
#pragma warning disable CA1031 // Docker 利用不可は複数の例外型で現れるため、Skip 判定として一括捕捉する（意図的例外）。
        catch (Exception ex)
        {
            SkipReason = $"OCI レジストリを起動できないため Skip します（Docker 未起動の可能性）: {ex.Message}";
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
