namespace Statevia.Core.Abstractions;

/// <summary>
/// コンパイラレイヤーが生成する、実行可能なワークフロー定義。
/// FSM 遷移テーブル、Fork テーブル、Join テーブルなどを含みます。
/// </summary>
public sealed class CompiledWorkflowDefinition
{
    /// <summary>ワークフロー名。</summary>
    public required string Name { get; init; }

    /// <summary>事実駆動 FSM 用の O(1) 遷移テーブル。(状態, 事実) → 遷移結果</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionTarget>> Transitions { get; init; }

    /// <summary>Fork テーブル。状態名 → 並列開始する状態の一覧。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> ForkTable { get; init; }

    /// <summary>Join テーブル。Join 状態名 → allOf で待つ状態の一覧。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> JoinTable { get; init; }

    /// <summary>Wait テーブル。状態名 → 待機するイベント名。</summary>
    public required IReadOnlyDictionary<string, string> WaitTable { get; init; }

    /// <summary>初期状態名。</summary>
    public required string InitialState { get; init; }

    /// <summary>状態名から IStateExecutor を取得するファクトリ。</summary>
    public required IStateExecutorFactory StateExecutorFactory { get; init; }
}

/// <summary>遷移の行き先。next / fork / end のいずれか。</summary>
public sealed class TransitionTarget
{
    /// <summary>次に遷移する状態名（next 遷移）。</summary>
    public string? Next { get; init; }

    /// <summary>並列開始する状態の一覧（fork 遷移）。</summary>
    public IReadOnlyList<string>? Fork { get; init; }

    /// <summary>ワークフロー終了を示すかどうか。</summary>
    public bool End { get; init; }
}
