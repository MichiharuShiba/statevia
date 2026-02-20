namespace Statevia.Core.Definition;

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
