using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Statevia.Infrastructure.Security;

/// <summary>パスワードのハッシュ化・検証。</summary>
internal sealed class PasswordCredentialService
{
    private readonly PasswordHasher<CredentialUser> _hasher = new();

    /// <summary>平文パスワードをハッシュする。</summary>
    /// <param name="password">平文。</param>
    /// <returns>保存用ハッシュ。</returns>
    public string HashPassword(string password) =>
        _hasher.HashPassword(CredentialUser.Instance, password);

    /// <summary>平文とハッシュを照合する。</summary>
    /// <param name="password">平文。</param>
    /// <param name="hash">保存済みハッシュ。</param>
    /// <returns>一致する場合 true。</returns>
    public bool VerifyPassword(string password, string hash) =>
        _hasher.VerifyHashedPassword(CredentialUser.Instance, hash, password) != PasswordVerificationResult.Failed;

    /// <summary>API キー平文から保存用ハッシュを生成する（平文は保存しない）。</summary>
    /// <param name="plainKey">平文キー。</param>
    /// <returns>Base64 SHA-256。</returns>
    public static string HashApiKey(string plainKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>API キーの表示用 prefix を返す。</summary>
    /// <param name="plainKey">平文キー。</param>
    /// <returns>先頭 8 文字（不足時は全体）。</returns>
    public static string ApiKeyPrefix(string plainKey) =>
        plainKey.Length <= 8 ? plainKey : plainKey[..8];

    /// <summary>新規 API キーの平文を生成する（保存は prefix + hash のみ）。</summary>
    /// <returns><c>stv_</c> プレフィックス付きの URL-safe 文字列。</returns>
    public static string GeneratePlainApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
        return $"stv_{encoded}";
    }

    private sealed class CredentialUser
    {
        public static readonly CredentialUser Instance = new();
        public string Id { get; } = "credential";
    }
}
