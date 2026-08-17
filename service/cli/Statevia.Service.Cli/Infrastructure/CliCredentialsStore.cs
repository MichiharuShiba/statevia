using System.Text.Json;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>CLI 資格情報ファイル（<c>~/.statevia/credentials</c>）の読み書き。</summary>
/// <remarks>
/// <para>既定パスは <see cref="CliHomePaths.CredentialsPath"/>。<c>STATEVIA_CREDENTIALS_FILE</c> は絶対パス上書きとして残す。</para>
/// <para>Unix ではファイル権限を所有者読み書き（0600）にする。Windows はユーザープロファイル配下への配置で代替する。</para>
/// </remarks>
public sealed class CliCredentialsStore
{
    internal const string CredentialsFileEnvironmentVariable = "STATEVIA_CREDENTIALS_FILE";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>解決済みの資格情報ファイルパス。</summary>
    public string FilePath { get; }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="filePath">資格情報ファイルパス。省略時は環境変数または既定パス。</param>
    public CliCredentialsStore(string? filePath = null)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath)
            ? ResolveDefaultPath()
            : Path.GetFullPath(filePath);
    }

    /// <summary>環境変数または <see cref="CliHomePaths"/> から既定パスを解決する。</summary>
    public static string ResolveDefaultPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(CredentialsFileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        return new CliHomePaths().CredentialsPath;
    }

    /// <summary>ファイルがあれば読み取る。欠落・不正 JSON は <see langword="null"/>。</summary>
    public CliCredentials? TryLoad()
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<CliCredentials>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>有効期限内の access token があれば返す。</summary>
    /// <param name="accessToken">有効なトークン。</param>
    /// <returns>有効なトークンがあるとき <see langword="true"/>。</returns>
    public bool TryGetValidAccessToken(out string? accessToken)
    {
        accessToken = null;
        var loaded = TryLoad();
        if (loaded is null || string.IsNullOrWhiteSpace(loaded.AccessToken))
            return false;

        if (loaded.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        accessToken = loaded.AccessToken;
        return true;
    }

    /// <summary>期限切れの資格情報ファイルがあるか。</summary>
    public bool HasExpiredAccessToken()
    {
        var loaded = TryLoad();
        return loaded is not null
            && !string.IsNullOrWhiteSpace(loaded.AccessToken)
            && loaded.ExpiresAt <= DateTimeOffset.UtcNow;
    }

    /// <summary>資格情報を保存する。</summary>
    /// <param name="credentials">保存する内容。</param>
    public void Save(CliCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        File.WriteAllText(FilePath, json);
        RestrictToOwner(FilePath);
    }

    /// <summary>資格情報ファイルを削除する。無いときは何もしない。</summary>
    public void Delete()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    private static void RestrictToOwner(string path) =>
        CliHomePaths.RestrictSecretFileToOwner(path);
}

/// <summary>CLI 資格情報ファイルの JSON 契約。</summary>
/// <param name="TenantKey">テナントキー。</param>
/// <param name="AccessToken">JWT アクセストークン。</param>
/// <param name="ExpiresAt">有効期限。</param>
/// <param name="ApiBase">ログイン時の API 基底 URL（任意）。</param>
public sealed record CliCredentials(
    string TenantKey,
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string? ApiBase);
