using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>ホーム <c>secrets</c>（API キー）の読み書き。</summary>
/// <remarks>
/// <para>Unix は 0600。Windows はユーザープロファイル配下への配置のみ（追加 ACL はしない）。</para>
/// <para>作業ディレクトリ配下の同名ファイルは見ない。値はログに出さない。</para>
/// </remarks>
public sealed class CliSecretsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CliHomePaths _homePaths;

    /// <summary>解決済みの secrets ファイルパス。</summary>
    public string FilePath => _homePaths.SecretsPath;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="homePaths">ホームパス。省略時は環境から解決。</param>
    public CliSecretsStore(CliHomePaths? homePaths = null)
    {
        _homePaths = homePaths ?? new CliHomePaths();
    }

    /// <summary>ホームの API キーがあれば返す。欠落は <see langword="null"/>。</summary>
    /// <returns>API キー。無い・空のときは <see langword="null"/>。</returns>
    /// <exception cref="CliHomeFileException">JSON 不正または既知キーの型不正。</exception>
    public string? TryGetApiKey()
    {
        var loaded = TryLoad();
        return string.IsNullOrWhiteSpace(loaded?.ApiKey) ? null : loaded.ApiKey.Trim();
    }

    /// <summary>API キーをホームへ保存する。</summary>
    /// <param name="apiKey">保存するキー。</param>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> が空。</exception>
    public void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key is required.", nameof(apiKey));

        Directory.CreateDirectory(_homePaths.HomeDirectory);
        var json = JsonSerializer.Serialize(new CliSecretsFile(apiKey.Trim()), JsonOptions);
        File.WriteAllText(FilePath, json);
        CliHomePaths.RestrictSecretFileToOwner(FilePath);
    }

    /// <summary>ホームの API キーを削除する。無いときは何もしない。</summary>
    public void DeleteApiKey()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    private CliSecretsFile? TryLoad()
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<CliSecretsFile>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw new CliHomeFileException(FilePath, "invalid JSON");
        }
        catch (IOException)
        {
            throw new CliHomeFileException(FilePath, "unreadable");
        }
    }
}

/// <summary>ホーム <c>secrets</c> の JSON 契約。</summary>
/// <param name="ApiKey">API キー。</param>
public sealed record CliSecretsFile(string? ApiKey = null);
