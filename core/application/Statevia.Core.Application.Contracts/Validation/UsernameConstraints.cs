using System.Text.RegularExpressions;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>ログインユーザー名の形式・長さ。</summary>
/// <remarks>
/// <para>許可は英大文字・小文字・数字と、途中のみのハイフン・アンダースコア・ドット。最大 64 文字。先頭・末尾の記号は不可。</para>
/// <para>一意性はテナント内で大小文字を区別する（DB の通常 collation）。</para>
/// </remarks>
public static class UsernameConstraints
{
    /// <summary>最大長。識別子として読みやすく、メールローカル部の上限より短くするため 64。</summary>
    public const int MaxLength = 64;

    /// <summary>許可文字の正規表現。先頭・末尾は英数字。</summary>
    public const string AllowedPattern = "^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$";

    /// <summary>形式違反時のメッセージ。</summary>
    public const string FormatErrorMessage =
        "username must be 1–64 letters or digits; hyphen, underscore, and dot are allowed only between them.";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>長さと文字種を満たすか。</summary>
    /// <param name="username">未 trim のユーザー名。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsValid(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        if (username.Length is 0 or > MaxLength)
            return false;

        return Regex.IsMatch(
            username,
            AllowedPattern,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            MatchTimeout);
    }
}
