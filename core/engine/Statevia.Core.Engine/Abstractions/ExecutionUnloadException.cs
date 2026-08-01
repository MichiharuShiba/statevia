namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// ホストが実行を unload するときに Wait 継続を打ち切るための例外。
/// グラフノードは未完了のまま残し、ProcessFact は走らせない。
/// </summary>
public sealed class ExecutionUnloadException : OperationCanceledException
{
    /// <summary>unload 打ち切りを表す例外を生成する。</summary>
    public ExecutionUnloadException()
        : base("Execution was unloaded by the host.")
    {
    }

    /// <summary>メッセージ付きで生成する。</summary>
    /// <param name="message">説明。</param>
    public ExecutionUnloadException(string message)
        : base(message)
    {
    }

    /// <summary>内部例外付きで生成する。</summary>
    /// <param name="message">説明。</param>
    /// <param name="innerException">原因例外。</param>
    public ExecutionUnloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
