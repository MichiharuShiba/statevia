using Microsoft.Extensions.Options;


namespace Statevia.Infrastructure.Modules;

/// <summary>OCI registry から Module artifact を取得して materialize する <see cref="IModuleSource"/>。</summary>
/// <remarks>
/// <para>
/// <see cref="MaterializingModuleSourceBase"/> 派生として、取得（<see cref="IOciArtifactFetcher"/>）→ 展開
/// （<see cref="ModuleZipInstaller"/> 再利用）→ entry DLL 解決を行い、ローカル正本 <see cref="MaterializedModule"/>
/// を生成する。最終 load・署名検証は ModuleHost が materialize 済みディレクトリに対して行う。
/// </para>
/// <para>
/// materialize 先は filesystem modules ルートと分離した OCI 専用キャッシュ（<see cref="OciModuleSourceOptions.CacheRoot"/>）
/// とし、filesystem Source による二重 discover を避ける。digest 単位のサブディレクトリへ展開し、再 reload の冪等性を保つ。
/// </para>
/// <para>
/// キャッシュ: 先に manifest digest を解決し、同一 digest の展開済みディレクトリがあればレイヤ blob の再取得をスキップする。
/// tag 参照は毎回 digest を解決するため、tag が別 digest へ移った場合はキャッシュミスとなり再取得する。
/// </para>
/// <para>
/// 信頼性: artifact 単位で例外を隔離し、1 件の取得・展開失敗は他 artifact の materialize を妨げない
/// （該当 Module のみ未登録とし、API は継続）。
/// </para>
/// </remarks>
internal sealed class OciModuleSource(
    IOptions<OciModuleSourceOptions> options,
    IOciArtifactFetcher fetcher,
    IHostEnvironment hostEnvironment,
    ILogger<OciModuleSource> logger) : MaterializingModuleSourceBase
{
    private readonly OciModuleSourceOptions _options = options.Value;

    /// <inheritdoc />
    public override int Priority => _options.Priority;

    /// <inheritdoc />
    protected override string SourceType => "oci";

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<MaterializedModule>> MaterializeModulesAsync(
        CancellationToken cancellationToken)
    {
        if (_options.Artifacts.Count == 0)
        {
            return Array.Empty<MaterializedModule>();
        }

        var cacheRoot = ResolveCacheRoot();
        var materialized = new List<MaterializedModule>();
        foreach (var artifact in _options.Artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await TryMaterializeAsync(artifact, cacheRoot, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                materialized.Add(result);
            }
        }

        return materialized;
    }

    private async Task<MaterializedModule?> TryMaterializeAsync(
        OciModuleArtifactOptions artifact,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifact.Registry)
            || string.IsNullOrWhiteSpace(artifact.Repository)
            || string.IsNullOrWhiteSpace(artifact.Reference))
        {
            OciModuleSourceLog.InvalidArtifactConfig(logger, artifact.Registry, artifact.Repository, artifact.Reference);
            return null;
        }

        var reference = new OciModuleReference(
            artifact.Registry,
            artifact.Repository,
            artifact.Reference,
            artifact.Username,
            artifact.Password,
            artifact.RefreshToken,
            artifact.PlainHttp);

        try
        {
            var digest = await ResolveDigestAsync(reference, cancellationToken).ConfigureAwait(false);
            var artifactCacheDir = Path.Combine(cacheRoot, SanitizeDigest(digest));
            if (TryReuseCachedModule(artifactCacheDir, reference, digest, out var cached))
            {
                return cached;
            }

            var fetched = await fetcher.FetchModuleAsync(reference, cancellationToken).ConfigureAwait(false);
            // tag 解決と pull の間に digest が変わった場合は pull 結果を正とする
            artifactCacheDir = Path.Combine(cacheRoot, SanitizeDigest(fetched.ManifestDigest));
            var moduleDirectory = MaterializeLayer(fetched.LayerZip, artifactCacheDir);
            var moduleDirectoryName = Path.GetFileName(moduleDirectory);

            if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out var reason))
            {
                OciModuleSourceLog.EntryUnresolved(logger, reference.Label, reason);
                return null;
            }

            return new MaterializedModule
            {
                ModuleDirectory = moduleDirectory,
                EntryAssemblyPath = entryAssemblyPath,
                SignaturePath = ResolveSignaturePath(moduleDirectory, moduleDirectoryName),
                SourceType = SourceType,
                SourceLabel = reference.Label,
                ContentDigest = fetched.ManifestDigest,
                MaterializedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OciModuleSourceLog.MaterializeFailed(logger, ex, reference.Label);
            return null;
        }
    }

    /// <summary>
    /// manifest digest を解決する。reference が既に digest 形式ならネットワークなしでそれを用い、
    /// それ以外（tag）は <see cref="IOciArtifactFetcher.ResolveManifestDigestAsync"/> で解決する。
    /// </summary>
    private Task<string> ResolveDigestAsync(OciModuleReference reference, CancellationToken cancellationToken) =>
        IsDigestReference(reference.Reference)
            ? Task.FromResult(reference.Reference)
            : fetcher.ResolveManifestDigestAsync(reference, cancellationToken);

    /// <summary>
    /// 同一 digest の展開済みキャッシュがあれば再利用する（レイヤ blob 再取得なし）。
    /// </summary>
    /// <returns>再利用できた場合 <see langword="true"/>。</returns>
    private bool TryReuseCachedModule(
        string artifactCacheDir,
        OciModuleReference reference,
        string digest,
        out MaterializedModule? cached)
    {
        cached = null;
        if (!Directory.Exists(artifactCacheDir))
        {
            return false;
        }

        var moduleDirectories = Directory.GetDirectories(artifactCacheDir);
        if (moduleDirectories.Length != 1)
        {
            return false;
        }

        var moduleDirectory = moduleDirectories[0];
        var moduleDirectoryName = Path.GetFileName(moduleDirectory);
        if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out _))
        {
            return false;
        }

        OciModuleSourceLog.CacheHit(logger, reference.Label, digest);
        cached = new MaterializedModule
        {
            ModuleDirectory = moduleDirectory,
            EntryAssemblyPath = entryAssemblyPath,
            SignaturePath = ResolveSignaturePath(moduleDirectory, moduleDirectoryName),
            SourceType = SourceType,
            SourceLabel = reference.Label,
            ContentDigest = digest,
            MaterializedAt = DateTimeOffset.UtcNow,
        };
        return true;
    }

    /// <summary>取得した配布 zip レイヤをキャッシュディレクトリへ安全展開し、module ディレクトリを返す。</summary>
    private static string MaterializeLayer(byte[] layerZip, string artifactCacheDir)
    {
        Directory.CreateDirectory(artifactCacheDir);

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"statevia-oci-{Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllBytes(tempZipPath, layerZip);
            return ModuleZipInstaller.Install(tempZipPath, artifactCacheDir);
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }
    }

    private string ResolveCacheRoot() =>
        string.IsNullOrWhiteSpace(_options.CacheRoot)
            ? Path.Combine(hostEnvironment.ContentRootPath, "oci-modules-cache")
            : Path.GetFullPath(_options.CacheRoot);

    /// <summary>module ディレクトリ直下の detached 署名ファイルがあれば絶対パスを返す。</summary>
    private static string? ResolveSignaturePath(string moduleDirectory, string moduleDirectoryName)
    {
        var signaturePath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.signature.json");
        return File.Exists(signaturePath) ? signaturePath : null;
    }

    /// <summary>digest を安全なディレクトリ名へ変換する（<c>sha256:...</c> の <c>:</c> を除去）。</summary>
    private static string SanitizeDigest(string digest) => digest.Replace(':', '-');

    /// <summary>reference が OCI digest（<c>sha256:...</c>）形式か。</summary>
    private static bool IsDigestReference(string reference) =>
        reference.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase);
}

/// <summary><see cref="OciModuleSource"/> の構造化ログ。機密（認証情報）は含めない。</summary>
internal static partial class OciModuleSourceLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipping OCI artifact with incomplete configuration: registry='{Registry}', repository='{Repository}', reference='{Reference}'")]
    public static partial void InvalidArtifactConfig(ILogger logger, string registry, string repository, string reference);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Skipping OCI module '{Reference}': entry assembly unresolved ({Reason})")]
    public static partial void EntryUnresolved(ILogger logger, string reference, string reason);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed to materialize OCI module '{Reference}'")]
    public static partial void MaterializeFailed(ILogger logger, Exception exception, string reference);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Reusing cached OCI module '{Reference}' (manifest digest {Digest}); skipping layer blob fetch")]
    public static partial void CacheHit(ILogger logger, string reference, string digest);
}
