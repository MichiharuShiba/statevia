using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 状態実行に渡されるコンテキスト。
/// Wait/Resume 用のイベント、完了済み状態の出力参照、ワークフロー ID などを提供します。
/// </summary>
public sealed class StateContext
{
    /// <summary>Wait で待機するイベントを提供します。Resume で再開できます。</summary>
    public required IEventProvider Events { get; init; }

    /// <summary>Join で参照する、完了済み状態の出力を読み取り専用で提供します。</summary>
    public required IReadOnlyStateStore Store { get; init; }

    /// <summary>ワークフローインスタンス ID。</summary>
    public required string WorkflowId { get; init; }

    /// <summary>現在実行中の状態名。</summary>
    public required string StateName { get; init; }

    /// <summary>
    /// ユーザー定義 State から利用できるロガー。
    /// 出力オブジェクトや workflowInput の生値を直接記録しないこと（IO-14）。
    /// </summary>
    public ILogger Logger { get; init; } = NullLogger.Instance;
}
