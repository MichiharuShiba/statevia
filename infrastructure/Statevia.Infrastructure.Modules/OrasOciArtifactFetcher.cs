using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Statevia.Infrastructure.Modules;

/// <summary>ORAS .NET（<c>OrasProject.Oras</c>）を用いた <see cref="IOciArtifactFetcher"/> 実装。</summary>
/// <remarks>
/// <para>
/// 取得ライブラリ依存（ORAS）を本クラスに封じ込める唯一の境界。ORAS は pre-1.0 のため、上位の
/// <see cref="OciModuleSource"/> は ORAS 型に依存せず本アダプタの <see cref="IOciArtifactFetcher"/> 契約のみに依存する。
/// </para>
/// <para>
/// 手順: registry へ接続 → manifest を fetch → Module レイヤ（<see cref="ModuleLayerMediaType"/>、無ければ単一レイヤ）
/// を選択 → blob を取得して bytes を返す。digest 検証は ORAS が担保する。
/// <see cref="ResolveManifestDigestAsync"/> は manifest のみを解決し、レイヤ blob を取得しない（digest キャッシュ判定用）。
/// </para>
/// <para>セキュリティ: 認証情報はログへ出力しない。token キャッシュはプロセス内メモリに保持する。</para>
/// </remarks>
internal sealed class OrasOciArtifactFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger<OrasOciArtifactFetcher> logger) : IOciArtifactFetcher, IDisposable
{
    /// <summary>Statevia Module 配布レイヤの media type。</summary>
    internal const string ModuleLayerMediaType = "application/vnd.statevia.module.layer.v1+zip";

    /// <summary>OCI 取得に用いる named HttpClient。</summary>
    internal const string HttpClientName = "oci-modules";

    private readonly MemoryCache _tokenCache = new(new MemoryCacheOptions());

    /// <inheritdoc />
    public async Task<string> ResolveManifestDigestAsync(
        OciModuleReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var repository = CreateRepository(reference);
        var (_, manifestDescriptor) = await FetchManifestAsync(repository, reference, cancellationToken)
            .ConfigureAwait(false);
        OrasOciArtifactFetcherLog.ManifestResolved(logger, reference.Label, manifestDescriptor.Digest);
        return manifestDescriptor.Digest;
    }

    /// <inheritdoc />
    public async Task<OciFetchedModule> FetchModuleAsync(OciModuleReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // manifest 取得とレイヤ取得で同一 Repository（同一 Client 接続）を共有する
        var repository = CreateRepository(reference);
        var (manifest, manifestDescriptor) = await FetchManifestAsync(repository, reference, cancellationToken)
            .ConfigureAwait(false);

        var layer = SelectModuleLayer(manifest, reference.Label);
        var layerBytes = await repository.FetchAllAsync(layer, cancellationToken).ConfigureAwait(false);

        OrasOciArtifactFetcherLog.ModuleFetched(logger, reference.Label, manifestDescriptor.Digest);
        return new OciFetchedModule(layerBytes, manifestDescriptor.Digest);
    }

    /// <summary>manifest を取得・検証し、デシリアライズ結果と descriptor を返す。</summary>
    /// <param name="repository">呼び出し側が用意した Repository（pull 内で再利用する）。</param>
    /// <param name="reference">取得対象の参照。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    private static async Task<(Manifest Manifest, Descriptor Descriptor)> FetchManifestAsync(
        Repository repository,
        OciModuleReference reference,
        CancellationToken cancellationToken)
    {
        var (manifestDescriptor, manifestStream) = await repository
            .FetchAsync(reference.Reference, cancellationToken)
            .ConfigureAwait(false);

        byte[] manifestBytes;
        await using (manifestStream.ConfigureAwait(false))
        {
            manifestBytes = await manifestStream.ReadAllAsync(manifestDescriptor, cancellationToken).ConfigureAwait(false);
        }

        if (manifestDescriptor.MediaType != MediaType.ImageManifest)
        {
            throw new InvalidOperationException(
                $"Unsupported OCI manifest media type '{manifestDescriptor.MediaType}'.");
        }

        var manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes)
            ?? throw new InvalidOperationException("Failed to deserialize OCI image manifest.");

        return (manifest, manifestDescriptor);
    }

    private Repository CreateRepository(OciModuleReference reference)
    {
        var credentialProvider = CreateCredentialProvider(reference);

        return new Repository(new RepositoryOptions
        {
            Reference = Reference.Parse($"{reference.Registry}/{reference.Repository}"),
            Client = new Client(httpClientFactory.CreateClient(HttpClientName), credentialProvider, new Cache(_tokenCache)),
            PlainHttp = reference.PlainHttp,
        });
    }

    /// <summary>
    /// 参照の認証情報から credential provider を生成する。
    /// </summary>
    /// <remarks>
    /// 認証情報が一切無い場合は匿名 provider を用いる。<see cref="SingleRegistryCredentialProvider"/> は空 credential を
    /// 受け付けないため、公開レジストリ等の匿名 pull では使えない。匿名 provider は解決時に空 credential を返し、
    /// ORAS の <see cref="Client"/> による匿名トークン取得経路を有効にする。
    /// </remarks>
    private static ICredentialProvider CreateCredentialProvider(OciModuleReference reference)
    {
        var hasCredential = !string.IsNullOrEmpty(reference.Username)
            || !string.IsNullOrEmpty(reference.Password)
            || !string.IsNullOrEmpty(reference.RefreshToken);
        if (!hasCredential)
        {
            return AnonymousCredentialProvider.Instance;
        }

        var credential = new Credential
        {
            Username = reference.Username ?? string.Empty,
            Password = reference.Password ?? string.Empty,
            RefreshToken = reference.RefreshToken ?? string.Empty,
        };
        return new SingleRegistryCredentialProvider(reference.Registry, credential);
    }

    /// <summary>Module レイヤを選択する。専用 media type を優先し、無ければ単一レイヤを採用する。</summary>
    private static Descriptor SelectModuleLayer(Manifest manifest, string label)
    {
        var moduleLayer = manifest.Layers
            .FirstOrDefault(layer => string.Equals(layer.MediaType, ModuleLayerMediaType, StringComparison.Ordinal));
        if (moduleLayer is not null)
        {
            return moduleLayer;
        }

        return manifest.Layers.Count switch
        {
            1 => manifest.Layers[0],
            0 => throw new InvalidOperationException($"OCI artifact '{label}' has no layers."),
            _ => throw new InvalidOperationException(
                $"OCI artifact '{label}' has multiple layers but none match '{ModuleLayerMediaType}'."),
        };
    }

    /// <inheritdoc />
    public void Dispose() => _tokenCache.Dispose();

    /// <summary>
    /// 解決時に空 credential を返す匿名 credential provider。公開レジストリからの匿名 pull に用いる。
    /// </summary>
    private sealed class AnonymousCredentialProvider : ICredentialProvider
    {
        /// <summary>共有インスタンス（状態を持たない）。</summary>
        public static readonly AnonymousCredentialProvider Instance = new();

        /// <inheritdoc />
        public Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Credential());
    }
}

internal static partial class OrasOciArtifactFetcherLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Fetched OCI module '{Reference}' (manifest digest {Digest})")]
    public static partial void ModuleFetched(ILogger logger, string reference, string digest);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Resolved OCI manifest for '{Reference}' (digest {Digest})")]
    public static partial void ManifestResolved(ILogger logger, string reference, string digest);
}
