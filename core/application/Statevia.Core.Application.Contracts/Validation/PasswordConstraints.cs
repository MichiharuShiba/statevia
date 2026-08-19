using System.Text.RegularExpressions;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>新規・更新時の平文パスワードの長さと文字種。</summary>
/// <remarks>
/// <para>最短 8・最長 128。空白（スペース・タブ・改行）は不可。英数字に加え記号を許可する。大文字と小文字の混在は必須にしない。</para>
/// <para>履歴照合や同一パスワード禁止は持たない。ログイン時の現行パスワードには適用しない。</para>
/// </remarks>
public static class PasswordConstraints
{
    /// <summary>最短長。推測耐性の下限として 8 を採用。</summary>
    public const int MinLength = 8;

    /// <summary>最長長。ハッシュ計算の過大入力を避けるため 128。</summary>
    public const int MaxLength = 128;

    /// <summary>空白以外の文字（長さは <see cref="IsValid"/> 側で判定）。</summary>
    public const string AllowedPattern = @"^\S+$";

    /// <summary>形式・長さ違反時のメッセージ。</summary>
    public const string FormatErrorMessage = "password must be 8–128 characters with no whitespace.";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>長さと空白なしを満たすか。</summary>
    /// <param name="password">未 trim の平文。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsValid(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length is < MinLength or > MaxLength)
            return false;

        return Regex.IsMatch(
            password,
            AllowedPattern,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            MatchTimeout);
    }
}

