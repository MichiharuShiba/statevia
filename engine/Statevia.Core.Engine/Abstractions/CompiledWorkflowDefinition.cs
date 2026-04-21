namespace Statevia.Core.Engine.Abstractions;

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

    /// <summary>条件遷移を含むコンパイル済み遷移テーブル。(状態, 事実) → 条件遷移定義</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>> ConditionalTransitions { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, CompiledFactTransition>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fork テーブル。状態名 → 並列開始する状態の一覧。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> ForkTable { get; init; }

    /// <summary>Join テーブル。Join 状態名 → allOf で待つ状態の一覧。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> JoinTable { get; init; }

    /// <summary>Wait テーブル。状態名 → 待機するイベント名。</summary>
    public required IReadOnlyDictionary<string, string> WaitTable { get; init; }

    /// <summary>状態ごとの入力指定（stateName → <see cref="Statevia.Core.Engine.Definition.StateInputDefinition"/>）。未定義状態は raw 通過。</summary>
    public IReadOnlyDictionary<string, Statevia.Core.Engine.Definition.StateInputDefinition> StateInputs { get; init; }
        = new System.Collections.Generic.Dictionary<string, Statevia.Core.Engine.Definition.StateInputDefinition>(System.StringComparer.OrdinalIgnoreCase);

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

/// <summary>
/// 1 つの事実遷移に対応するコンパイル済み表現。
/// 従来の線形遷移（next/fork/end）と条件遷移（cases/default）の両方を保持する。
/// </summary>
public sealed class CompiledFactTransition
{
    /// <summary>線形遷移（next/fork/end）。条件遷移を使う場合は null。</summary>
    public TransitionTarget? LinearTarget { get; init; }

    /// <summary>評価順に並んだ条件ケース。</summary>
    public IReadOnlyList<CompiledTransitionCase> Cases { get; init; } = [];

    /// <summary>条件不一致時のフォールバック遷移。</summary>
    public TransitionTarget? DefaultTarget { get; init; }
}

/// <summary>条件ケースのコンパイル済み表現。</summary>
public sealed class CompiledTransitionCase
{
    /// <summary>評価順（指定ありは昇順、未指定は指定ありの後）に使う優先度。</summary>
    public int? Order { get; init; }

    /// <summary>同一 order でのタイブレークに使う元の定義順。</summary>
    public int DeclarationIndex { get; init; }

    /// <summary>成立条件。</summary>
    public required Statevia.Core.Engine.Definition.ConditionExpressionDefinition When { get; init; }

    /// <summary>条件一致時の遷移先。</summary>
    public required TransitionTarget Target { get; init; }
}
