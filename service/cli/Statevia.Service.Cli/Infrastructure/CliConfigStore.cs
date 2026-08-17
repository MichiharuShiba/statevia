using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Service.Cli.Infrastructure;

/// <summary>ホーム <c>config</c>（非秘密 JSON）の読み書き。</summary>
/// <remarks>
/// <para>作業ディレクトリ配下の同名ファイルは見ない。</para>
/// <para>欠落は未設定。不正 JSON / 既知キーの型不正は <see cref="CliHomeFileException"/>。</para>
/// </remarks>
public sealed class CliConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CliHomePaths _homePaths;

    /// <summary>解決済みの config ファイルパス。</summary>
    public string FilePath => _homePaths.ConfigPath;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="homePaths">ホームパス。省略時は環境から解決。</param>
    public CliConfigStore(CliHomePaths? homePaths = null)
    {
        _homePaths = homePaths ?? new CliHomePaths();
    }

    /// <summary>ファイルがあれば読む。欠落は <see langword="null"/>。</summary>
    /// <returns>読み取った設定。ファイルが無いときは <see langword="null"/>。</returns>
    /// <exception cref="CliHomeFileException">JSON 不正または既知キーの型不正。</exception>
    public CliConfigFile? TryLoad()
    {
        if (!File.Exists(FilePath))
            return null;

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<CliConfigFile>(json, JsonOptions) ?? new CliConfigFile();
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

    /// <summary>非秘密設定をホームへ書き込む。</summary>
    /// <param name="file">保存する内容。</param>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> が null。</exception>
    public void Save(CliConfigFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        Directory.CreateDirectory(_homePaths.HomeDirectory);

        if (file.IsEmpty)
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            return;
        }

        var json = JsonSerializer.Serialize(file, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}

/// <summary>ホーム <c>config</c> の JSON 契約。</summary>
/// <param name="ApiBase">Service API の絶対 URL。</param>
/// <param name="Tenant">テナントキー。</param>
/// <param name="ModulesPath">modules ルート。</param>
public sealed record CliConfigFile(
    string? ApiBase = null,
    string? Tenant = null,
    string? ModulesPath = null)
{
    /// <summary>保存対象のキーがすべて空か。</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(ApiBase)
        && string.IsNullOrWhiteSpace(Tenant)
        && string.IsNullOrWhiteSpace(ModulesPath);
}
