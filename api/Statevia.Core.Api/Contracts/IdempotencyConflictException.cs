namespace Statevia.Core.Api.Contracts;

/// <summary>
/// 同一の <c>X-Idempotency-Key</c> に対し、以前と異なるリクエスト内容が送られたときにスローする。
/// HTTP 409 に写像する。
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }
}
