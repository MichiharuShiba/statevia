namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>連絡先メールの長さ上限。</summary>
/// <remarks>ログイン識別子ではない。形式は API の <c>EmailAddress</c> 検証に委譲する。</remarks>
public static class UserEmailConstraints
{
    /// <summary>最大長。一般的なメール欄の上限に合わせ 256。</summary>
    public const int MaxLength = 256;
}
