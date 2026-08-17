namespace Statevia.Service.Cli.Infrastructure;

/// <summary>CLI ホーム（<c>STATEVIA_HOME</c>）と配下ファイルパスを一箇所で解決する。</summary>
/// <remarks>
/// <para>既定は <c>{UserProfile}/.statevia</c>。作業ディレクトリ相対の <c>.statevia/</c> は使わない。</para>
/// <para>テストと CI は <c>STATEVIA_HOME</c> を一時ディレクトリへ向けて隔離する。</para>
/// </remarks>
public sealed class CliHomePaths
{
    /// <summary>ホームディレクトリを差し替える環境変数。</summary>
    internal const string HomeEnvironmentVariable = "STATEVIA_HOME";

    /// <summary>解決済みのホームディレクトリ。</summary>
    public string HomeDirectory { get; }

    /// <summary>非秘密設定ファイル（<c>config</c>）のパス。</summary>
    public string ConfigPath => Path.Combine(HomeDirectory, "config");

    /// <summary>API キー秘密ファイル（<c>secrets</c>）のパス。</summary>
    public string SecretsPath => Path.Combine(HomeDirectory, "secrets");

    /// <summary>JWT 資格情報ファイル（<c>credentials</c>）の既定パス。</summary>
    public string CredentialsPath => Path.Combine(HomeDirectory, "credentials");

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="homeDirectory">ホームディレクトリ。省略時は環境変数またはユーザープロファイル。</param>
    public CliHomePaths(string? homeDirectory = null)
    {
        HomeDirectory = string.IsNullOrWhiteSpace(homeDirectory)
            ? ResolveHomeDirectory()
            : Path.GetFullPath(homeDirectory);
    }

    /// <summary>環境変数またはユーザープロファイルからホームディレクトリを解決する。</summary>
    /// <returns>絶対パスのホームディレクトリ。</returns>
    public static string ResolveHomeDirectory()
    {
        var overrideHome = Environment.GetEnvironmentVariable(HomeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideHome))
            return Path.GetFullPath(overrideHome);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".statevia");
    }

    /// <summary>Unix で秘密ファイルを所有者読み書きのみ（0600）にする。Windows では何もしない。</summary>
    /// <param name="path">対象ファイル。</param>
    internal static void RestrictSecretFileToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
