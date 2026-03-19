using System;
using System.Collections.Generic;

namespace Statevia.Core.Api.Contracts;

/// <summary>Execution Read Model（UI 向け正規形）。</summary>
public sealed class ExecutionReadModel
{
    public string ExecutionId { get; init; } = string.Empty;
    public string GraphId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? CancelRequestedAt { get; init; }
    public DateTimeOffset? CanceledAt { get; init; }
    public DateTimeOffset? FailedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }

    // TODO: nodes の詳細は ExecutionGraph の仕様に合わせて埋める（現状は空コレクションを返す実装になる想定）。
    public IReadOnlyList<ExecutionNodeReadModel> Nodes { get; init; } =
        Array.Empty<ExecutionNodeReadModel>();
}

public sealed class ExecutionNodeReadModel
{
    public string NodeId { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Attempt { get; init; }
    public string? WorkerId { get; init; }
    public string? WaitKey { get; init; }

    // TODO: canceledByExecution は ExecutionGraph 側に情報を持たせた上でマッピングする。
    public bool CanceledByExecution { get; init; }
}

