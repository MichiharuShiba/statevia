namespace Statevia.Core.Application.Contracts;

/// <summary>
/// 422（入力不正）で details を返したい場合の例外。
/// </summary>
public sealed class ApiValidationException : ArgumentException
{
    /// <summary>
    /// <see cref="ApiValidationException"/> を生成する。
    /// </summary>
    public ApiValidationException()
        : this("The request was invalid.", null, null)
    {
    }

    /// <summary>
    /// <see cref="ApiValidationException"/> を生成する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    public ApiValidationException(string message)
        : this(message, null, null)
    {
    }

    /// <summary>
    /// <see cref="ApiValidationException"/> を生成する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public ApiValidationException(string message, Exception innerException)
        : this(message, null, innerException)
    {
    }

    /// <summary>
    /// <see cref="ApiValidationException"/> を生成する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="details">追加詳細。</param>
    public ApiValidationException(string message, object? details)
        : this(message, details, null)
    {
    }

    /// <summary>
    /// <see cref="ApiValidationException"/> を生成する。
    /// </summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="details">追加詳細（任意）。</param>
    /// <param name="innerException">内部例外（任意）。</param>
    public ApiValidationException(string message, object? details, Exception? innerException)
        : base(message, innerException)
    {
        Details = details;
    }

    /// <summary>フィールド別エラー等の追加情報。</summary>
    public object? Details { get; }
}
