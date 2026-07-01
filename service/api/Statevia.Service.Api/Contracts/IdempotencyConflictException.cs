namespace Statevia.Service.Api.Contracts;

/// <summary>
/// 同一の <c>X-Idempotency-Key</c> に対し、以前と異なるリクエスト内容が送られたときにスローする。
/// HTTP 409 に写像する。
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    /// <summary>
    /// <see cref="IdempotencyConflictException"/> を生成する。
    /// </summary>
    public IdempotencyConflictException()
        : this("The idempotency key conflicts with a previous request.")
    {
    }

    /// <summary>
    /// <see cref="IdempotencyConflictException"/> を生成する。
    /// </summary>
    /// <param name="message">説明メッセージ。</param>
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// <see cref="IdempotencyConflictException"/> を生成する。
    /// </summary>
    /// <param name="message">説明メッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public IdempotencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
