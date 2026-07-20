using System.ComponentModel.DataAnnotations;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>
/// 空白のみの文字列を検証失敗にする属性。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RequiredAttribute"/> は null / 空文字を拒否するが、空白のみ（例: <c>" "</c>）は通す。
/// 本属性は HTTP 入力の形式検証でその穴を埋める。
/// </para>
/// <para>
/// <c>null</c> は本属性では検証しない（必須は <see cref="RequiredAttribute"/> に委譲）。
/// 非 <see cref="string"/> は検証対象外（成功扱い）。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
public sealed class NotWhitespaceAttribute : ValidationAttribute
{
    /// <summary>
    /// <see cref="NotWhitespaceAttribute"/> を生成する。
    /// </summary>
    public NotWhitespaceAttribute()
        : base("The field {0} must not be whitespace only.")
    {
    }

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        return value is not string text || !string.IsNullOrWhiteSpace(text);
    }
}
