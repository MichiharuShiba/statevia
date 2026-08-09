using System.Text.Json;

namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 再開可能な実行ランタイム状態のチェックポイント（観測用 <see cref="ExecutionSnapshot"/> とは別）。
/// </summary>
/// <remarks>
/// <para>Engine は永続化を知らない。ホストが JSON 等で保存し、Import 時に復元する。</para>
/// <para><see cref="SchemaVersion"/> で後方互換マイグレーションを行う。</para>
/// </remarks>
public sealed class ExecutionRuntimeCheckpoint
{
    /// <summary>チェックポイント JSON スキーマ版（現行は 1）。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>スキーマ版。</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>実行インスタンス ID。</summary>
    public required string ExecutionId { get; init; }

    /// <summary>定義名（Import 時の <see cref="CompiledWorkflowDefinition.Name"/> と照合）。</summary>
    public required string DefinitionName { get; init; }

    /// <summary>終端フラグ。</summary>
    public bool IsCompleted { get; init; }

    /// <summary>キャンセル済みフラグ。</summary>
    public bool IsCancelled { get; init; }

    /// <summary>失敗フラグ。</summary>
    public bool IsFailed { get; init; }

    /// <summary>アクティブ状態名。</summary>
    public required IReadOnlyList<string> ActiveStates { get; init; }

    /// <summary>状態試行回数（stateName → attempt）。</summary>
    public required IReadOnlyDictionary<string, int> StateAttempts { get; init; }

    /// <summary>状態出力（stateName → JSON）。</summary>
    public required IReadOnlyDictionary<string, JsonElement?> StateOutputs { get; init; }

    /// <summary>Publish 冪等に使った clientEventId。</summary>
    public required IReadOnlyList<Guid> AppliedPublishClientEventIds { get; init; }

    /// <summary>Cancel 冪等に使った clientEventId。</summary>
    public required IReadOnlyList<Guid> AppliedCancelClientEventIds { get; init; }

    /// <summary>Execution Context 断面。</summary>
    public required CheckpointContextData Context { get; init; }

    /// <summary>実行グラフ断面。</summary>
    public required CheckpointGraphData Graph { get; init; }

    /// <summary>JoinTracker 可変状態。</summary>
    public required CheckpointJoinData Join { get; init; }

    /// <summary>未完了 Wait（nodeId + 許可イベント）。</summary>
    public required IReadOnlyList<CheckpointPendingWait> PendingWaits { get; init; }
}

/// <summary>チェックポイント内の Execution Context。</summary>
public sealed class CheckpointContextData
{
    /// <summary>開始 input。</summary>
    public JsonElement? Input { get; init; }

    /// <summary>ワークフロー output。</summary>
    public JsonElement? WorkflowOutput { get; init; }

    /// <summary>vars。</summary>
    public JsonElement? Vars { get; init; }

    /// <summary>states（stateName → { output }）。</summary>
    public required IReadOnlyDictionary<string, JsonElement?> States { get; init; }
}

/// <summary>チェックポイント内の実行グラフ。</summary>
public sealed class CheckpointGraphData
{
    /// <summary>ノード一覧。</summary>
    public required IReadOnlyList<CheckpointGraphNode> Nodes { get; init; }

    /// <summary>辺一覧。</summary>
    public required IReadOnlyList<CheckpointGraphEdge> Edges { get; init; }
}

/// <summary>グラフノード断面。</summary>
public sealed class CheckpointGraphNode
{
    /// <summary>ノード ID（復元時に維持）。</summary>
    public required string NodeId { get; init; }

    /// <summary>ノード名。定義上の StateName と同値。</summary>
    public required string NodeName { get; init; }

    /// <summary>ノード種別。</summary>
    public required string NodeType { get; init; }

    /// <summary>開始時刻 UTC。</summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>完了時刻 UTC。</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>完了事実。</summary>
    public string? Fact { get; init; }

    /// <summary>出力 JSON。</summary>
    public JsonElement? Output { get; init; }

    /// <summary>入力 JSON。</summary>
    public JsonElement? Input { get; init; }

    /// <summary>試行回数。</summary>
    public int Attempt { get; init; }

    /// <summary>ワーカー ID。</summary>
    public string? WorkerId { get; init; }

    /// <summary>単一イベント互換キー。</summary>
    public string? WaitKey { get; init; }

    /// <summary>許可イベント。</summary>
    public IReadOnlyList<string>? AllowedEvents { get; init; }

    /// <summary>Wait.subscribe 購読一覧。</summary>
    public IReadOnlyList<CheckpointWaitSubscription>? Subscriptions { get; init; }

    /// <summary>実行キャンセル由来か。</summary>
    public bool CanceledByExecution { get; init; }
}

/// <summary>グラフ辺断面。</summary>
public sealed class CheckpointGraphEdge
{
    /// <summary>始点。</summary>
    public required string From { get; init; }

    /// <summary>終点。</summary>
    public required string To { get; init; }

    /// <summary>辺種別名（<c>Next</c> 等）。</summary>
    public required string Type { get; init; }
}

/// <summary>JoinTracker 断面。</summary>
public sealed class CheckpointJoinData
{
    /// <summary>joinState → sourceState → 観測結果。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, CheckpointJoinObserved>> JoinStateResults { get; init; }

    /// <summary>joinState → sourceState → nodeId。</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> JoinSourceNodeIds { get; init; }

    /// <summary>開始済み Join 状態名。</summary>
    public required IReadOnlyList<string> StartedJoins { get; init; }
}

/// <summary>Join 依存の観測結果。</summary>
public sealed class CheckpointJoinObserved
{
    /// <summary>事実。</summary>
    public required string Fact { get; init; }

    /// <summary>出力 JSON。</summary>
    public JsonElement? Output { get; init; }
}

/// <summary>未完了 Wait の登録情報。</summary>
public sealed class CheckpointPendingWait
{
    /// <summary>Wait ノード ID。</summary>
    public required string NodeId { get; init; }

    /// <summary>ノード名。定義上の StateName と同値。</summary>
    public required string NodeName { get; init; }

    /// <summary>許可イベント。</summary>
    public required IReadOnlyList<string> AllowedEvents { get; init; }

    /// <summary>Wait.subscribe 購読一覧。</summary>
    public IReadOnlyList<CheckpointWaitSubscription>? Subscriptions { get; init; }
}

/// <summary>チェックポイント上の Wait 購読エントリ。</summary>
public sealed class CheckpointWaitSubscription
{
    /// <summary>購読トピック。</summary>
    public required string Topic { get; init; }

    /// <summary>相関キー（未指定は空文字）。</summary>
    public required string Key { get; init; }

    /// <summary>Resume 用内部イベント名。</summary>
    public required string ResumeEventName { get; init; }
}
