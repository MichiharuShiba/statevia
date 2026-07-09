namespace Statevia.Core.Application.Contracts;

/// <summary>
/// リソースの現在状態と要求操作が矛盾するときにスローする。HTTP 409 に写像する。
/// </summary>
public sealed class StateConflictException : Exception
{
    /// <summary><see cref="StateConflictException"/> を生成する。</summary>
    public StateConflictException()
        : this("The resource is not in the expected state for this operation.")
    {
    }

    /// <summary><see cref="StateConflictException"/> を生成する。</summary>
    /// <param name="message">説明メッセージ。</param>
    public StateConflictException(string message)
        : base(message)
    {
    }

    /// <summary><see cref="StateConflictException"/> を生成する。</summary>
    /// <param name="message">説明メッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public StateConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
