using System.Text.Json;

namespace Statevia.Core.Api.Contracts;

/// <summary>UI <c>WorkflowView</c> に近い形（camelCase JSON）。GET …/state 等で返す。</summary>
public sealed class WorkflowViewDto
{
    /// <summary>表示用ワークフロー ID。</summary>
    public string DisplayId { get; init; } = string.Empty;

    /// <summary>ワークフローのリソース UUID。</summary>
    public string ResourceId { get; init; } = string.Empty;

    /// <summary>定義グラフ ID（定義の resource_id の文字列化）。</summary>
    public string GraphId { get; init; } = string.Empty;

    /// <summary>ワークフロー状態文字列（Running 等）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>開始日時（UTC）。</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>最終更新日時（UTC）。未更新なら <see langword="null"/>。</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>キャンセル要求が出ているか。</summary>
    public bool CancelRequested { get; init; }

    /// <summary>再起動でランタイム状態が失われた疑いがあるか。</summary>
    public bool RestartLost { get; init; }

    /// <summary>実行ノードの一覧。</summary>
    public IReadOnlyList<WorkflowViewNodeDto> Nodes { get; init; } = Array.Empty<WorkflowViewNodeDto>();
}

/// <summary>ワークフロービュー上の 1 ノード。</summary>
public sealed class WorkflowViewNodeDto
{
    /// <summary>ExecutionGraph が付与するノード識別子（試行単位）。定義キャンバスのノードキーとは別。</summary>
    public string ExecutionNodeId { get; init; } = string.Empty;

    /// <summary>ワークフロー定義上の状態名（<see cref="ExecutionNodeId"/> とは別）。</summary>
    public string StateName { get; init; } = string.Empty;

    /// <summary>ノード種別（例: Action, Wait）。</summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>ノードの実行状態。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>試行番号（1 始まり）。</summary>
    public int Attempt { get; init; }

    /// <summary>ワーカー識別子（任意）。</summary>
    public string? WorkerId { get; init; }

    /// <summary>Wait 解除キー（任意）。</summary>
    public string? WaitKey { get; init; }

    /// <summary>実行キャンセルにより打ち切られたか。</summary>
    public bool CanceledByExecution { get; init; }

    /// <summary>ノード入力（デバッグ用途。機密の可能性あり）。</summary>
    public JsonElement? Input { get; init; }

    /// <summary>ノード出力（デバッグ用途。機密の可能性あり）。</summary>
    public JsonElement? Output { get; init; }

    /// <summary>条件遷移の評価情報（ExecutionGraph の conditionRouting をそのまま透過）。</summary>
    public JsonElement? ConditionRouting { get; init; }
}

/// <summary>GET …/events のレスポンス（UI <c>ExecutionEventsResponse</c>）。</summary>
public sealed class ExecutionEventsResponseDto
{
    /// <summary>タイムラインイベントの列。</summary>
    public IReadOnlyList<TimelineEventDto> Events { get; init; } = Array.Empty<TimelineEventDto>();

    /// <summary>さらに後続イベントがあるか。</summary>
    public bool HasMore { get; init; }
}

/// <summary>タイムライン／SSE 用のイベント（UI の <c>ExecutionStreamEvent</c> + seq）。</summary>
public sealed class TimelineEventDto
{
    /// <summary>event_store 上のシーケンス番号。</summary>
    public long Seq { get; init; }

    /// <summary>イベント種別。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>実行（ワークフロー表示）ID。</summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>遷移先ノード ID（任意）。</summary>
    public string? To { get; init; }

    /// <summary>遷移元ノード ID（任意）。</summary>
    public string? From { get; init; }

    /// <summary>グラフ差分パッチ（GraphUpdated 時）。</summary>
    public GraphUpdatedPatchDto? Patch { get; init; }

    /// <summary>イベント発生時刻の ISO 文字列（任意）。</summary>
    public string? At { get; init; }
}

/// <summary>GraphUpdated イベント内の patch 本体。</summary>
public sealed class GraphUpdatedPatchDto
{
    /// <summary>更新されたノードの列（任意）。</summary>
    public IReadOnlyList<GraphPatchNodeDto>? Nodes { get; init; }
}

/// <summary>グラフ差分の 1 ノード分。</summary>
public sealed class GraphPatchNodeDto
{
    /// <summary>実行ノード ID。</summary>
    public string ExecutionNodeId { get; init; } = string.Empty;

    /// <summary>状態名（任意）。</summary>
    public string? StateName { get; init; }

    /// <summary>ノード状態（任意）。</summary>
    public string? Status { get; init; }

    /// <summary>試行番号（任意）。</summary>
    public int? Attempt { get; init; }

    /// <summary>ワーカー ID（任意）。</summary>
    public string? WorkerId { get; init; }

    /// <summary>Wait キー（任意）。</summary>
    public string? WaitKey { get; init; }

    /// <summary>実行キャンセルフラグ（任意）。</summary>
    public bool? CanceledByExecution { get; init; }
}

/// <summary>POST …/nodes/…/resume のリクエスト本文。</summary>
public sealed class ResumeNodeRequest
{
    /// <summary>Wait を再開するイベント名（Engine.PublishEvent に渡す）。</summary>
    public string? ResumeKey { get; init; }
}
