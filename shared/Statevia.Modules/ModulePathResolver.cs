namespace Statevia.Modules;

/// <summary>modules ルートの優先順位付き解決（ASP.NET 非依存）。</summary>
public static class ModulePathResolver
{
    /// <summary><c>STATEVIA_MODULES_PATH</c> 環境変数名。</summary>
    public const string EnvironmentVariable = "STATEVIA_MODULES_PATH";

    /// <summary>appsettings 等の設定キー（<c>Statevia:Modules:Path</c>）。</summary>
    public const string ConfigurationKey = "Statevia:Modules:Path";

    /// <summary>content root 配下の既定 modules ディレクトリ名。</summary>
    public const string DefaultRelativeDirectory = "modules";

    /// <summary>
    /// modules ルートの絶対パスを解決する。
    /// 優先順: 環境変数 → 設定 → <c>{contentRoot}/modules</c>。
    /// </summary>
    /// <param name="contentRootPath">アプリケーション content root（API は <c>ContentRootPath</c>）。</param>
    /// <param name="environmentPath"><see cref="EnvironmentVariable"/> の値（未設定は null）。</param>
    /// <param name="configurationPath">設定ファイル由来のパス（未設定は null）。</param>
    /// <returns>絶対パス。</returns>
    public static string Resolve(
        string contentRootPath,
        string? environmentPath,
        string? configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configured = FirstNonWhitespace(environmentPath) ?? FirstNonWhitespace(configurationPath);
        var rawPath = configured ?? Path.Combine(contentRootPath, DefaultRelativeDirectory);

        return Path.IsPathRooted(rawPath)
            ? Path.GetFullPath(rawPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, rawPath));
    }

    private static string? FirstNonWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
