using System.IO.Compression;
using Microsoft.Extensions.Options;

namespace Statevia.Infrastructure.Modules;

/// <summary>Git ホストから Module artifact を取得して materialize する <see cref="IModuleSource"/>。</summary>
/// <remarks>
/// <para>
/// HTTP archive（GitHub / GitLab）で commit 解決 → archive 取得 → ModulePath 抽出 →
/// entry 解決までを担う。最終 load・署名検証は ModuleHost が行う。
/// </para>
/// <para>
/// キャッシュキーは commit SHA + ModulePath。同一キーの展開済みディレクトリがあれば archive 再取得をスキップする。
/// </para>
/// <para>信頼性: artifact 単位で例外を隔離する。</para>
/// </remarks>
internal sealed class GitModuleSource(
    IOptions<GitModuleSourceOptions> options,
    IGitArtifactFetcher fetcher,
    IHostEnvironment hostEnvironment,
    ILogger<GitModuleSource> logger) : MaterializingModuleSourceBase
{
    private readonly GitModuleSourceOptions _options = options.Value;

    /// <inheritdoc />
    public override int Priority => _options.Priority;

    /// <inheritdoc />
    protected override string SourceType => "git";

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
        GitModuleArtifactOptions artifact,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artifact.Host)
            || string.IsNullOrWhiteSpace(artifact.Owner)
            || string.IsNullOrWhiteSpace(artifact.Repository)
            || string.IsNullOrWhiteSpace(artifact.Ref)
            || string.IsNullOrWhiteSpace(artifact.ModulePath))
        {
            GitModuleSourceLog.InvalidArtifactConfig(
                logger,
                artifact.Host,
                artifact.Owner,
                artifact.Repository,
                artifact.Ref,
                artifact.ModulePath);
            return null;
        }

        string provider;
        try
        {
            provider = GitModuleProviders.Resolve(artifact.Provider, artifact.Host);
        }
        catch (ArgumentException ex)
        {
            GitModuleSourceLog.InvalidProvider(logger, artifact.Provider ?? string.Empty, ex.Message);
            return null;
        }

        var reference = new GitModuleReference(
            artifact.Host.Trim(),
            artifact.Owner.Trim(),
            artifact.Repository.Trim(),
            artifact.Ref.Trim(),
            artifact.ModulePath.Trim(),
            provider,
            artifact.Token,
            artifact.PlainHttp);

        try
        {
            var commitSha = await fetcher.ResolveCommitShaAsync(reference, cancellationToken).ConfigureAwait(false);
            var identity = BuildContentIdentity(commitSha, reference.ModulePath);
            var artifactCacheDir = Path.Combine(cacheRoot, SanitizeIdentity(identity));
            if (TryReuseCachedModule(artifactCacheDir, reference, identity, out var cached))
            {
                return cached;
            }

            var archiveBytes = await fetcher.FetchArchiveAsync(reference, commitSha, cancellationToken)
                .ConfigureAwait(false);
            var moduleDirectory = GitArchiveMaterializer.Materialize(
                archiveBytes,
                reference.ModulePath,
                artifactCacheDir);
            var moduleDirectoryName = Path.GetFileName(moduleDirectory);

            if (!TryResolveEntryAssemblyPath(moduleDirectory, moduleDirectoryName, out var entryAssemblyPath, out var reason))
            {
                GitModuleSourceLog.EntryUnresolved(logger, reference.Label, reason);
                return null;
            }

            return new MaterializedModule
            {
                ModuleDirectory = moduleDirectory,
                EntryAssemblyPath = entryAssemblyPath,
                SignaturePath = ResolveSignaturePath(moduleDirectory, moduleDirectoryName),
                SourceType = SourceType,
                SourceLabel = reference.Label,
                ContentDigest = identity,
                MaterializedAt = DateTimeOffset.UtcNow,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GitModuleSourceLog.MaterializeFailed(logger, ex, reference.Label);
            return null;
        }
    }

    /// <summary>同一 content identity の展開済みキャッシュがあれば再利用する。</summary>
    private bool TryReuseCachedModule(
        string artifactCacheDir,
        GitModuleReference reference,
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

        GitModuleSourceLog.CacheHit(logger, reference.Label, identity);
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

    private string ResolveCacheRoot() =>
        string.IsNullOrWhiteSpace(_options.CacheRoot)
            ? Path.Combine(hostEnvironment.ContentRootPath, "git-modules-cache")
            : Path.GetFullPath(_options.CacheRoot);

    private static string? ResolveSignaturePath(string moduleDirectory, string moduleDirectoryName)
    {
        var signaturePath = Path.Combine(moduleDirectory, $"{moduleDirectoryName}.signature.json");
        return File.Exists(signaturePath) ? signaturePath : null;
    }

    /// <summary>キャッシュキー用 content identity（commit SHA + ModulePath）。</summary>
    internal static string BuildContentIdentity(string commitSha, string modulePath) =>
        $"sha:{commitSha}+path:{NormalizeModulePathKey(modulePath)}";

    private static string NormalizeModulePathKey(string modulePath) =>
        modulePath.Replace('\\', '/').Trim().Trim('/');

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

/// <summary>
/// Git リポジトリ archive から ModulePath を安全に取り出し、キャッシュ配下へ materialize する。
/// </summary>
/// <remarks>
/// archive 全体は展開せず、ModulePath 配下（または単一 .zip entry）のみを抽出する。
/// パストラバーサル（<c>..</c>）は拒否する。
/// </remarks>
internal static class GitArchiveMaterializer
{
    private const long MaxEntryUncompressedBytes = 32L * 1024 * 1024;
    private const long MaxArchiveUncompressedBytes = 64L * 1024 * 1024;

    /// <summary>archive zip bytes から Module を materialize し、module ディレクトリ絶対パスを返す。</summary>
    public static string Materialize(byte[] archiveZip, string modulePath, string artifactCacheDir)
    {
        ArgumentNullException.ThrowIfNull(archiveZip);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactCacheDir);

        var normalizedModulePath = NormalizeModulePath(modulePath);
        Directory.CreateDirectory(artifactCacheDir);

        var tempArchivePath = Path.Combine(Path.GetTempPath(), $"statevia-git-{Guid.NewGuid():N}.zip");
        try
        {
            File.WriteAllBytes(tempArchivePath, archiveZip);
            using var archive = ZipFile.OpenRead(tempArchivePath);
            if (archive.Entries.Count == 0)
            {
                throw new InvalidOperationException("Git archive is empty.");
            }

            var topLevel = ResolveSingleTopLevelPrefix(archive);
            var archiveRelative = string.IsNullOrEmpty(topLevel)
                ? normalizedModulePath
                : $"{topLevel}/{normalizedModulePath}";

            return IsZipModulePath(normalizedModulePath)
                ? MaterializeZipEntry(archive, archiveRelative, artifactCacheDir)
                : MaterializeDirectoryEntries(archive, archiveRelative, normalizedModulePath, artifactCacheDir);
        }
        finally
        {
            if (File.Exists(tempArchivePath))
            {
                File.Delete(tempArchivePath);
            }
        }
    }

    private static string MaterializeZipEntry(ZipArchive archive, string archiveRelative, string artifactCacheDir)
    {
        var entry = archive.GetEntry(archiveRelative)
            ?? archive.Entries.FirstOrDefault(e =>
                string.Equals(NormalizeEntryPath(e.FullName), archiveRelative, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Module zip entry '{archiveRelative}' was not found in Git archive.");

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"statevia-git-mod-{Guid.NewGuid():N}.zip");
        try
        {
            ExtractEntryToFile(entry, tempZipPath);
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

    private static string MaterializeDirectoryEntries(
        ZipArchive archive,
        string archiveRelativePrefix,
        string normalizedModulePath,
        string artifactCacheDir)
    {
        var moduleDirectoryName = Path.GetFileName(normalizedModulePath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(moduleDirectoryName))
        {
            throw new InvalidOperationException("Unable to determine module directory name from ModulePath.");
        }

        var targetDirectory = Path.GetFullPath(Path.Combine(artifactCacheDir, moduleDirectoryName));
        EnsureUnderRoot(artifactCacheDir, targetDirectory);

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);

        var prefix = archiveRelativePrefix.TrimEnd('/') + "/";
        var matched = archive.Entries
            .Where(entry =>
            {
                var full = NormalizeEntryPath(entry.FullName);
                return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(entry.Name);
            })
            .ToList();

        if (matched.Count == 0)
        {
            throw new InvalidOperationException(
                $"Module directory '{archiveRelativePrefix}' was not found in Git archive.");
        }

        long archiveBytesExtracted = 0;
        foreach (var entry in matched)
        {
            var full = NormalizeEntryPath(entry.FullName);
            var relative = full[prefix.Length..];
            if (relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(static s => s is "." or ".."))
            {
                throw new InvalidOperationException($"Zip entry '{entry.FullName}' contains path traversal.");
            }

            var destinationPath = Path.GetFullPath(
                Path.Combine(targetDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
            EnsureUnderRoot(targetDirectory, destinationPath);

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            ExtractEntrySafely(entry, destinationPath, ref archiveBytesExtracted);
        }

        return targetDirectory;
    }

    private static void ExtractEntryToFile(ZipArchiveEntry entry, string destinationPath)
    {
        long archiveBytesExtracted = 0;
        ExtractEntrySafely(entry, destinationPath, ref archiveBytesExtracted);
    }

    private static void ExtractEntrySafely(ZipArchiveEntry entry, string destinationPath, ref long archiveBytesExtracted)
    {
        if (entry.Length < 0)
        {
            throw new InvalidOperationException($"Zip entry '{entry.FullName}' has unknown uncompressed size.");
        }

        if (entry.Length > MaxEntryUncompressedBytes)
        {
            throw new InvalidOperationException($"Zip entry '{entry.FullName}' exceeds maximum allowed size.");
        }

        using var entryStream = entry.Open();
        using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.SequentialScan);

        var buffer = new byte[81920];
        long entryBytesExtracted = 0;
        int bytesRead;
        while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            entryBytesExtracted += bytesRead;
            if (entryBytesExtracted > MaxEntryUncompressedBytes)
            {
                throw new InvalidOperationException($"Zip entry '{entry.FullName}' exceeds maximum allowed size.");
            }

            archiveBytesExtracted += bytesRead;
            if (archiveBytesExtracted > MaxArchiveUncompressedBytes)
            {
                throw new InvalidOperationException("Git module payload exceeds maximum allowed uncompressed size.");
            }

            fileStream.Write(buffer, 0, bytesRead);
        }
    }

    private static string ResolveSingleTopLevelPrefix(ZipArchive archive)
    {
        var topLevels = archive.Entries
            .Select(entry => GetTopLevelSegment(entry.FullName))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return topLevels.Count == 1 ? topLevels[0] : string.Empty;
    }

    private static string GetTopLevelSegment(string entryFullName)
    {
        var normalized = NormalizeEntryPath(entryFullName);
        var separatorIndex = normalized.IndexOf('/');
        return separatorIndex < 0 ? normalized : normalized[..separatorIndex];
    }

    private static string NormalizeEntryPath(string entryFullName) =>
        entryFullName.Replace('\\', '/').TrimStart('/');

    private static string NormalizeModulePath(string modulePath)
    {
        var normalized = modulePath.Replace('\\', '/').Trim().Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(static s => s is "." or ".."))
        {
            throw new InvalidOperationException($"Invalid ModulePath '{modulePath}'.");
        }

        return string.Join("/", segments);
    }

    private static bool IsZipModulePath(string normalizedModulePath) =>
        normalizedModulePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static void EnsureUnderRoot(string root, string candidate)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullCandidate = Path.GetFullPath(candidate);
        if (!fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullCandidate, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{candidate}' escapes allowed root '{root}'.");
        }
    }
}

/// <summary><see cref="GitModuleSource"/> の構造化ログ。機密（Token）は含めない。</summary>
internal static partial class GitModuleSourceLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipping Git artifact with incomplete configuration: host='{Host}', owner='{Owner}', repository='{Repository}', ref='{Ref}', modulePath='{ModulePath}'")]
    public static partial void InvalidArtifactConfig(
        ILogger logger,
        string host,
        string owner,
        string repository,
        string @ref,
        string modulePath);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Skipping Git artifact with unsupported provider '{Provider}': {Reason}")]
    public static partial void InvalidProvider(ILogger logger, string provider, string reason);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Skipping Git module '{Reference}': entry assembly unresolved ({Reason})")]
    public static partial void EntryUnresolved(ILogger logger, string reference, string reason);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Failed to materialize Git module '{Reference}'")]
    public static partial void MaterializeFailed(ILogger logger, Exception exception, string reference);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Reusing cached Git module '{Reference}' (content identity {Identity}); skipping archive fetch")]
    public static partial void CacheHit(ILogger logger, string reference, string identity);
}
