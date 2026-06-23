using System.IO.Compression;

namespace Statevia.Modules;

/// <summary>
/// Action Module 配布 zip を modules ルート配下へ安全に展開するユーティリティ。
/// </summary>
/// <remarks>
/// <para>
/// CLI の <c>statevia module install</c> から呼び出される。展開結果は
/// <c>{modulesRoot}/{moduleDirectoryName}/</c> となり、Core-API の filesystem module scan の対象になる。
/// </para>
/// <para><b>module ディレクトリ名の決定</b></para>
/// <list type="bullet">
/// <item>zip 内のトップレベルが単一ディレクトリのみ → その名前を使用。entry 先頭の同名プレフィックスは剥がして展開（二重ネスト防止）。</item>
/// <item>それ以外（ルート直下の複数ファイル、複数トップレベル） → zip ファイル名（拡張子除く）を使用。</item>
/// </list>
/// <para><b>セキュリティ</b></para>
/// <list type="bullet">
/// <item>パストラバーサル（<c>..</c> / <c>.</c> セグメント、modules ルート外・module ディレクトリ外への脱出）を拒否。</item>
/// <item>zip bomb 対策として展開後サイズを制限（entry あたり 32 MiB、archive 合計 64 MiB）。中央ディレクトリと実展開バイトの乖離はストリーム読み込み中にも検証。</item>
/// <item>非圧縮サイズが未知（<see cref="ZipArchiveEntry.Length"/> &lt; 0）の entry は拒否。</item>
/// </list>
/// <para>同一 module ディレクトリが既に存在する場合は再帰削除のうえ上書き展開する。</para>
/// </remarks>
public static class ModuleZipInstaller
{
    /// <summary>
    /// zip を modules ルート配下へ展開する。
    /// </summary>
    /// <param name="zipFilePath">zip ファイルの絶対または相対パス。</param>
    /// <param name="modulesRoot">modules ルートの絶対または相対パス。存在しない場合は作成する。</param>
    /// <returns>展開先 module ディレクトリの絶対パス。</returns>
    /// <exception cref="ArgumentException"><paramref name="zipFilePath"/> または <paramref name="modulesRoot"/> が空。</exception>
    /// <exception cref="FileNotFoundException"><paramref name="zipFilePath"/> が存在しない。</exception>
    /// <exception cref="InvalidOperationException">空 zip、展開先の決定不能、パストラバーサル、サイズ上限超過。</exception>
    public static string Install(string zipFilePath, string modulesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modulesRoot);

        var fullZipPath = Path.GetFullPath(zipFilePath);
        if (!File.Exists(fullZipPath))
        {
            throw new FileNotFoundException($"Module zip not found: '{fullZipPath}'.", fullZipPath);
        }

        var fullModulesRoot = Path.GetFullPath(modulesRoot);
        Directory.CreateDirectory(fullModulesRoot);

        using var archive = ZipFile.OpenRead(fullZipPath);
        if (archive.Entries.Count == 0)
        {
            throw new InvalidOperationException("Module zip is empty.");
        }

        var moduleDirectoryName = ResolveModuleDirectoryName(archive, fullZipPath);
        var stripTopLevelPrefix = ShouldStripTopLevelPrefix(archive, moduleDirectoryName);
        var targetDirectory = Path.GetFullPath(Path.Combine(fullModulesRoot, moduleDirectoryName));
        if (!targetDirectory.StartsWith(fullModulesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetDirectory, fullModulesRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved module directory escapes modules root.");
        }

        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);

        ValidateArchiveUncompressedSize(archive);

        var archiveBytesExtracted = 0L;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var relativePath = NormalizeEntryRelativePath(entry.FullName, stripTopLevelPrefix);
            var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, relativePath));
            if (!destinationPath.StartsWith(targetDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(destinationPath, targetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Zip entry '{entry.FullName}' escapes module directory.");
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            ExtractEntrySafely(entry, destinationPath, ref archiveBytesExtracted);
        }

        return targetDirectory;
    }

    /// <summary>1 zip entry の展開後サイズ上限（32 MiB）。</summary>
    /// <remarks>Action Module（entry DLL + 私有依存）の実用的上限。zip bomb 対策。</remarks>
    private const long MaxEntryUncompressedBytes = 32L * 1024 * 1024;

    /// <summary>1 zip 全体の展開後サイズ上限（64 MiB）。</summary>
    /// <remarks>複数 entry の合算に対する zip bomb 対策。</remarks>
    private const long MaxArchiveUncompressedBytes = 64L * 1024 * 1024;

    /// <summary>中央ディレクトリに記録された非圧縮サイズの合計が archive 上限以内か検証する。</summary>
    /// <param name="archive">読み取り中の zip。</param>
    private static void ValidateArchiveUncompressedSize(ZipArchive archive)
    {
        long totalUncompressedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var entryLength = entry.Length;
            if (entryLength < 0)
            {
                throw new InvalidOperationException($"Zip entry '{entry.FullName}' has unknown uncompressed size.");
            }

            if (entryLength > MaxEntryUncompressedBytes)
            {
                throw new InvalidOperationException($"Zip entry '{entry.FullName}' exceeds maximum allowed size.");
            }

            totalUncompressedBytes += entryLength;
            if (totalUncompressedBytes > MaxArchiveUncompressedBytes)
            {
                throw new InvalidOperationException("Module zip exceeds maximum allowed uncompressed size.");
            }
        }
    }

    /// <summary>
    /// 1 entry をストリーム展開し、entry / archive 双方のサイズ上限を逐次検証する。
    /// </summary>
    /// <param name="entry">展開対象 entry。</param>
    /// <param name="destinationPath">書き込み先ファイルパス。</param>
    /// <param name="archiveBytesExtracted">これまでに展開した archive 合計バイト数（呼び出し側で累積）。</param>
    private static void ExtractEntrySafely(ZipArchiveEntry entry, string destinationPath, ref long archiveBytesExtracted)
    {
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
                throw new InvalidOperationException("Module zip exceeds maximum allowed uncompressed size.");
            }

            fileStream.Write(buffer, 0, bytesRead);
        }
    }

    /// <summary>
    /// 展開先 module ディレクトリ名を zip 構成から決定する。
    /// </summary>
    /// <param name="archive">読み取り中の zip。</param>
    /// <param name="zipFilePath">zip の絶対パス（フラット構成時のフォールバック名に使用）。</param>
    /// <returns>modules ルート直下に作成するディレクトリ名。</returns>
    private static string ResolveModuleDirectoryName(ZipArchive archive, string zipFilePath)
    {
        var topLevelNames = archive.Entries
            .Select(entry => GetTopLevelSegment(entry.FullName))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (topLevelNames.Count == 1)
        {
            return topLevelNames[0];
        }

        var zipBaseName = Path.GetFileNameWithoutExtension(zipFilePath);
        if (string.IsNullOrWhiteSpace(zipBaseName))
        {
            throw new InvalidOperationException("Unable to determine module directory name from zip.");
        }

        return zipBaseName.Trim();
    }

    /// <summary>entry パスの先頭セグメント（トップレベルディレクトリまたはファイル名）を返す。</summary>
    private static string GetTopLevelSegment(string entryFullName)
    {
        var normalized = entryFullName.Replace('\\', '/').Trim('/');
        var slashIndex = normalized.IndexOf('/', StringComparison.Ordinal);
        return slashIndex < 0 ? normalized : normalized[..slashIndex];
    }

    /// <summary>
    /// 単一トップレベルディレクトリ zip のとき、entry 先頭の同名プレフィックスを剥がすか判定する。
    /// </summary>
    /// <param name="archive">読み取り中の zip。</param>
    /// <param name="moduleDirectoryName"><see cref="ResolveModuleDirectoryName"/> の結果。</param>
    private static bool ShouldStripTopLevelPrefix(ZipArchive archive, string moduleDirectoryName)
    {
        var topLevelNames = archive.Entries
            .Select(entry => GetTopLevelSegment(entry.FullName))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return topLevelNames.Count == 1
            && string.Equals(topLevelNames[0], moduleDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// zip entry を module ディレクトリ内の相対パスへ正規化する。
    /// </summary>
    /// <param name="entryFullName">zip 内の entry フルパス。</param>
    /// <param name="stripTopLevelPrefix">先頭の module ディレクトリ名プレフィックスを除去するか。</param>
    /// <returns>OS パス区切りに変換した相対パス。</returns>
    /// <exception cref="InvalidOperationException"><c>.</c> または <c>..</c> セグメントを含む。</exception>
    private static string NormalizeEntryRelativePath(string entryFullName, bool stripTopLevelPrefix)
    {
        var normalized = entryFullName.Replace('\\', '/').TrimStart('/');
        if (stripTopLevelPrefix)
        {
            var slashIndex = normalized.IndexOf('/', StringComparison.Ordinal);
            if (slashIndex >= 0)
            {
                normalized = normalized[(slashIndex + 1)..];
            }
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Zip entry '{entryFullName}' contains invalid path segments.");
        }

        return Path.Combine(segments);
    }
}
