using System;
using System.Collections.Generic;

namespace Statevia.Core.Api.Contracts;

/// <summary>Execution Read Model（UI 向け正規形）。</summary>
public sealed class ExecutionReadModel
{
    /// <summary>実行 ID（表示用）。</summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>グラフ（定義）ID。</summary>
    public string GraphId { get; init; } = string.Empty;

    /// <summary>実行全体の状態。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>キャンセル要求日時（任意）。</summary>
    public DateTimeOffset? CancelRequestedAt { get; init; }

    /// <summary>キャンセル完了日時（任意）。</summary>
    public DateTimeOffset? CanceledAt { get; init; }

    /// <summary>失敗日時（任意）。</summary>
    public DateTimeOffset? FailedAt { get; init; }

    /// <summary>完了日時（任意）。</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    // TODO: nodes の詳細は ExecutionGraph の仕様に合わせて埋める（現状は空コレクションを返す実装になる想定）。
    /// <summary>実行ノードの列。</summary>
    public IReadOnlyList<ExecutionNodeReadModel> Nodes { get; init; } =
        Array.Empty<ExecutionNodeReadModel>();
}

/// <summary>実行読み取りモデル上の 1 ノード。</summary>
public sealed class ExecutionNodeReadModel
{
    /// <summary>ExecutionGraph のノード識別子（試行単位）。</summary>
    public string ExecutionNodeId { get; init; } = string.Empty;

    /// <summary>ノード種別。</summary>
    public string NodeType { get; init; } = string.Empty;

    /// <summary>ノードの状態。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>試行番号。</summary>
    public int Attempt { get; init; }

    /// <summary>ワーカー ID（任意）。</summary>
    public string? WorkerId { get; init; }

    /// <summary>Wait キー（任意）。</summary>
    public string? WaitKey { get; init; }

    // TODO: canceledByExecution は ExecutionGraph 側に情報を持たせた上でマッピングする。
    /// <summary>実行キャンセルにより打ち切られたか。</summary>
    public bool CanceledByExecution { get; init; }
}
