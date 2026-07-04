namespace Statevia.Core.Application.Contracts;

/// <summary>
/// リソースが存在しないことを表す例外。
/// API は ExceptionFilter で 404 に写像する。
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>
    /// <see cref="NotFoundException"/> を生成する。
    /// </summary>
    public NotFoundException()
        : this("The resource was not found.")
    {
    }

    /// <summary>
    /// <see cref="NotFoundException"/> を生成する。
    /// </summary>
    /// <param name="message">説明メッセージ。</param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// <see cref="NotFoundException"/> を生成する。
    /// </summary>
    /// <param name="message">説明メッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
