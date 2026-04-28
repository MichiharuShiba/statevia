namespace Statevia.Core.Api.Contracts;

/// <summary>
/// 422（入力不正）で details を返したい場合の例外。
/// </summary>
public sealed class ApiValidationException : ArgumentException
{
    public ApiValidationException(string message, object? details = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Details = details;
    }

    public object? Details { get; }
}
