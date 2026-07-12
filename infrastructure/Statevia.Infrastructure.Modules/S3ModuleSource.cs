using Microsoft.Extensions.Options;

namespace Statevia.Infrastructure.Modules;

/// <summary>S3 互換ストレージから Module artifact を取得して materialize する <see cref="IModuleSource"/>。</summary>
/// <remarks>
/// <para>
/// <see cref="MaterializingModuleSourceBase"/> 派生として、取得（<see cref="IS3ArtifactFetcher"/>）→ 展開
/// （<see cref="ModuleZipInstaller"/>）→ entry DLL 解決を行い、ローカル正本 <see cref="MaterializedModule"/>
/// を生成する。最終 load・署名検証は ModuleHost が行う。
/// </para>
/// <para>
/// materialize 先は filesystem / OCI と分離した S3 専用キャッシュ（<see cref="S3ModuleSourceOptions.CacheRoot"/>）。
/// content identity 単位のサブディレクトリへ展開し、再 reload の冪等性と blob 再取得抑止を保つ。
/// </para>
/// <para>
/// 信頼性: artifact 単位で例外を隔離し、1 件の失敗は他 artifact の materialize を妨げない。
/// </para>
/// </remarks>
internal sealed class S3ModuleSource(
    IOptions<S3ModuleSourceOptions> options,
    IS3ArtifactFetcher fetcher,
    IHostEnvironment hostEnvironment,
    ILogger<S3ModuleSource> logger) : MaterializingModuleSourceBase
{
    private readonly S3ModuleSourceOptions _options = options.Value;

    /// <inheritdoc />
    public override int Priority => _options.Priority;

    /// <inheritdoc />
    protected override string SourceType => "s3";

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
        S3ModuleArtifactOptions artifact,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifact.Bucket) || string.IsNullOrWhiteSpace(artifact.Key))
        {
            S3ModuleSourceLog.InvalidArtifactConfig(logger, artifact.Bucket, artifact.Key);
            return null;
        }

        var reference = new S3ModuleReference(
            artifact.Bucket,
            artifact.Key,
            artifact.Region,
            artifact.ServiceUrl,
            artifact.AccessKeyId,
            artifact.SecretAccessKey,
            artifact.VersionId);

        try
        {
            var identity = await fetcher.ResolveContentIdentityAsync(reference, cancellationToken).ConfigureAwait(false);
            var artifactCacheDir = Path.Combine(cacheRoot, SanitizeIdentity(identity));
            if (TryReuseCachedModule(artifactCacheDir, reference, identity, out var cached))
            {
                return cached;
            }

            var fetched = await fetcher.FetchModuleAsync(reference, cancellationToken).ConfigureAwait(false);
            artifactCacheDir = Path.Combine(cacheRoot, SanitizeIdentity(fetched.ContentIdentity));
            var moduleDirectory = MaterializeZip(fetched.ZipBytes, artifactCacheDir);
            var moduleDirectoryName = Path.GetFileName(moduleDirectory);

            if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out var reason))
            {
                S3ModuleSourceLog.EntryUnresolved(logger, reference.Label, reason);
                return null;
            }

            return new MaterializedModule
            {
                ModuleDirectory = moduleDirectory,
                EntryAssemblyPath = entryAssemblyPath,
                SignaturePath = ResolveSignaturePath(moduleDirectory, moduleDirectoryName),
                SourceType = SourceType,
                SourceLabel = reference.Label,
                ContentDigest = fetched.ContentIdentity,
                MaterializedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            S3ModuleSourceLog.MaterializeFailed(logger, ex, reference.Label);
            return null;
        }
    }

    /// <summary>同一 content identity の展開済みキャッシュがあれば再利用する。</summary>
    private bool TryReuseCachedModule(
        string artifactCacheDir,
        S3ModuleReference reference,
        string identity,
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

        S3ModuleSourceLog.CacheHit(logger, reference.Label, identity);
        cached = new MaterializedModule
        {
            ModuleDirectory = moduleDirectory,
            EntryAssemblyPath = entryAssemblyPath,
            SignaturePath = ResolveSignaturePath(moduleDirectory, moduleDirectoryName),
            SourceType = SourceType,
            SourceLabel = reference.Label,
            ContentDigest = identity,
            MaterializedAt = DateTimeOffset.UtcNow,
        };
        return true;
    }

    private static string MaterializeZip(byte[] zipBytes, string artifactCacheDir)
    {
        Directory.CreateDirectory(artifactCacheDir);

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"statevia-s3-{Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllBytes(tempZipPath, zipBytes);
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
            ? Path.Combine(hostEnvironment.ContentRootPath, "s3-modules-cache")
            : Path.GetFullPath(_options.CacheRoot);

    private static string? ResolveSignaturePath(string moduleDirectory, string moduleDirectoryName)
    {
        var signaturePath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.signature.json");
        return File.Exists(signaturePath) ? signaturePath : null;
    }

    /// <summary>content identity を安全なディレクトリ名へ変換する。</summary>
    private static string SanitizeIdentity(string identity)
    {
        var sanitized = identity;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '-');
        }

        return sanitized.Replace(':', '-').Replace('/', '-').Replace('\\', '-').Replace('+', '-');
    }
}

/// <summary><see cref="S3ModuleSource"/> の構造化ログ。機密（認証情報）は含めない。</summary>
internal static partial class S3ModuleSourceLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipping S3 artifact with incomplete configuration: bucket='{Bucket}', key='{Key}'")]
    public static partial void InvalidArtifactConfig(ILogger logger, string bucket, string key);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Skipping S3 module '{Reference}': entry assembly unresolved ({Reason})")]
    public static partial void EntryUnresolved(ILogger logger, string reference, string reason);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed to materialize S3 module '{Reference}'")]
    public static partial void MaterializeFailed(ILogger logger, Exception exception, string reference);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Reusing cached S3 module '{Reference}' (content identity {Identity}); skipping blob fetch")]
    public static partial void CacheHit(ILogger logger, string reference, string identity);
}
