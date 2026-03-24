namespace Statevia.Core.Engine.Definition;

/// <summary>ロードされたワークフロー定義。workflow メタデータと states を含みます。</summary>
public sealed class WorkflowDefinition
{
    /// <summary>ワークフローのメタデータ。</summary>
    public required WorkflowMetadata Workflow { get; init; }
    /// <summary>状態名 → 状態定義のマップ。</summary>
    public required IReadOnlyDictionary<string, StateDefinition> States { get; init; }
}

/// <summary>ワークフロー全体のメタデータ。</summary>
public sealed class WorkflowMetadata
{
    /// <summary>ワークフロー名。</summary>
    public required string Name { get; init; }
}

/// <summary>単一状態の定義。on（事実→遷移）、wait、join を含みます。</summary>
public sealed class StateDefinition
{
    /// <summary>事実駆動 FSM の遷移定義（事実名 → 遷移）。</summary>
    public IReadOnlyDictionary<string, TransitionDefinition>? On { get; init; }
    /// <summary>Wait/Resume 用の待機イベント定義。</summary>
    public WaitDefinition? Wait { get; init; }
    /// <summary>Join の allOf 依存定義。</summary>
    public JoinDefinition? Join { get; init; }
    /// <summary>遷移でこの状態に入る直前に候補 input へ適用する指定（YAML キーは <c>input</c>）。</summary>
    public StateInputDefinition? Input { get; init; }
}

/// <summary>遷移の定義。next / fork / end のいずれか。</summary>
public sealed class TransitionDefinition
{
    /// <summary>次状態名（next 遷移）。</summary>
    public string? Next { get; init; }
    /// <summary>Fork で並列開始する状態の一覧。</summary>
    public IReadOnlyList<string>? Fork { get; init; }
    /// <summary>ワークフロー終了かどうか。</summary>
    public bool End { get; init; }
}

/// <summary>Wait で待機するイベントの定義。</summary>
public sealed class WaitDefinition
{
    /// <summary>イベント名。</summary>
    public required string Event { get; init; }
}

/// <summary>Join の allOf 依存定義。</summary>
public sealed class JoinDefinition
{
    /// <summary>完了を待つ状態名の一覧。</summary>
    public required IReadOnlyList<string> AllOf { get; init; }
}

/// <summary>
/// 状態入力の最小指定（states.<name>.input）。
/// 現在は JSONPath 風の <c>path</c>（例: <c>$.foo.bar</c>）のみをサポートする。
/// </summary>
public sealed class StateInputDefinition
{
    /// <summary>入力候補から値を抽出するパス（単一ショートハンド）。</summary>
    public string? Path { get; init; }

    /// <summary>
    /// 複数キーの入力指定（key -> expression/literal）。
    /// key にドットを含む場合はネストオブジェクトとして構築される。
    /// </summary>
    public IReadOnlyDictionary<string, StateInputValueDefinition>? Values { get; init; }
}

/// <summary>
/// 入力値定義。<see cref="Path"/> が設定されていれば raw から抽出し、
/// 未設定なら <see cref="Literal"/> をそのまま使う。
/// </summary>
public sealed class StateInputValueDefinition
{
    /// <summary>JSONPath 風パス（$ または $.a.b）。</summary>
    public string? Path { get; init; }
    /// <summary>リテラル値（string/number/bool/null/object/array）。</summary>
    public object? Literal { get; init; }
}
