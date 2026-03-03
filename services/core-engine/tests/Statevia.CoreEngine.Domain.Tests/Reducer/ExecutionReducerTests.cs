using Statevia.CoreEngine.Domain.Events;
using Statevia.CoreEngine.Domain.Execution;
using Statevia.CoreEngine.Domain.Node;
using Statevia.CoreEngine.Domain.Reducer;

namespace Statevia.CoreEngine.Domain.Tests.Reducer;

public static class ReducerTestHelpers
{
    private static readonly Actor SystemActor = new(ActorKind.System);

    public static EventEnvelope CreateEvent(
        string type,
        string executionId,
        IReadOnlyDictionary<string, object?>? payload = null,
        string? occurredAt = null)
    {
        return new EventEnvelope(
            EventId: Guid.NewGuid().ToString(),
            ExecutionId: executionId,
            Type: type,
            OccurredAt: occurredAt ?? DateTime.UtcNow.ToString("O"),
            Actor: SystemActor,
            SchemaVersion: 1,
            Payload: payload ?? new Dictionary<string, object?>());
    }

    public static ExecutionState CreateInitialState(string executionId, string graphId = "graph-1")
    {
        return new ExecutionState(
            executionId,
            graphId,
            ExecutionStatus.ACTIVE,
            new Dictionary<string, NodeState>(),
            Version: 0);
    }

    public static IReadOnlyDictionary<string, object?> Payload(params (string key, object? value)[] pairs)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (key, value) in pairs)
            d[key] = value;
        return d;
    }
}

public class ExecutionReducerTests
{
    /// <summary>EXECUTION_CREATED 適用で graphId が payload からセットされ、status が ACTIVE になること。</summary>
    [Fact]
    public void EXECUTION_CREATED_sets_graphId_and_status_ACTIVE()
    {
        // Arrange: 空の graphId の初期状態と EXECUTION_CREATED イベント
        var state = ReducerTestHelpers.CreateInitialState("exec-1", "");
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.ExecutionCreated,
            "exec-1",
            ReducerTestHelpers.Payload(("graphId", "graph-1")));

        // Act: Reducer を適用
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert: graphId と status が期待どおり
        Assert.Equal("graph-1", result.GraphId);
        Assert.Equal(ExecutionStatus.ACTIVE, result.Status);
    }

    /// <summary>EXECUTION_STARTED 適用で status が ACTIVE のままになること。</summary>
    [Fact]
    public void EXECUTION_STARTED_sets_status_ACTIVE()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = ReducerTestHelpers.CreateEvent(EventTypeConstants.ExecutionStarted, "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(ExecutionStatus.ACTIVE, result.Status);
    }

    /// <summary>EXECUTION_CANCEL_REQUESTED 初回で cancelRequestedAt にイベント発生日時が入ること。</summary>
    [Fact]
    public void EXECUTION_CANCEL_REQUESTED_sets_cancelRequestedAt_on_first_request()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var occurredAt = "2024-01-01T12:00:00Z";
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.ExecutionCancelRequested,
            "exec-1",
            null,
            occurredAt);

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(occurredAt, result.CancelRequestedAt);
    }

    /// <summary>既に cancelRequestedAt があるとき、EXECUTION_CANCEL_REQUESTED で上書きされないこと。</summary>
    [Fact]
    public void EXECUTION_CANCEL_REQUESTED_does_not_overwrite_existing_cancelRequestedAt()
    {
        // Arrange: 既存の cancelRequestedAt を持つ状態
        var existingTime = "2024-01-01T00:00:00Z";
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with { CancelRequestedAt = existingTime };
        var envelope = ReducerTestHelpers.CreateEvent(EventTypeConstants.ExecutionCancelRequested, "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(existingTime, result.CancelRequestedAt);
    }

    /// <summary>EXECUTION_CANCELED 適用で canceledAt と status が CANCELED になること。</summary>
    [Fact]
    public void EXECUTION_CANCELED_sets_canceledAt_and_status_CANCELED()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var occurredAt = "2024-01-01T12:00:00Z";
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.ExecutionCanceled,
            "exec-1",
            null,
            occurredAt);

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(occurredAt, result.CanceledAt);
        Assert.Equal(ExecutionStatus.CANCELED, result.Status);
    }

    /// <summary>EXECUTION_CANCELED 適用時、未終端ノードが CANCELED + canceledByExecution に normalize され、既に終端のノードはそのままであること。</summary>
    [Fact]
    public void EXECUTION_CANCELED_normalizes_all_active_nodes_to_CANCELED_with_canceledByExecution()
    {
        // Arrange: IDLE/READY/RUNNING/WAITING/SUCCEEDED の5ノード
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.IDLE, 0),
                ["node-2"] = new NodeState("node-2", "task", NodeStatus.READY, 0),
                ["node-3"] = new NodeState("node-3", "task", NodeStatus.RUNNING, 1),
                ["node-4"] = new NodeState("node-4", "task", NodeStatus.WAITING, 1),
                ["node-5"] = new NodeState("node-5", "task", NodeStatus.SUCCEEDED, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(EventTypeConstants.ExecutionCanceled, "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert: アクティブ4つは CANCELED + canceledByExecution、SUCCEEDED はそのまま
        Assert.Equal(ExecutionStatus.CANCELED, result.Status);
        Assert.Equal(NodeStatus.CANCELED, result.Nodes["node-1"].Status);
        Assert.True(result.Nodes["node-1"].CanceledByExecution);
        Assert.Equal(NodeStatus.CANCELED, result.Nodes["node-2"].Status);
        Assert.Equal(NodeStatus.CANCELED, result.Nodes["node-3"].Status);
        Assert.Equal(NodeStatus.CANCELED, result.Nodes["node-4"].Status);
        Assert.Equal(NodeStatus.SUCCEEDED, result.Nodes["node-5"].Status);
    }

    /// <summary>EXECUTION_FAILED 適用で failedAt と status が FAILED になること。</summary>
    [Fact]
    public void EXECUTION_FAILED_sets_failedAt_and_status_FAILED()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = ReducerTestHelpers.CreateEvent(EventTypeConstants.ExecutionFailed, "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.NotNull(result.FailedAt);
        Assert.Equal(ExecutionStatus.FAILED, result.Status);
    }

    /// <summary>EXECUTION_COMPLETED 適用で completedAt と status が COMPLETED になること。</summary>
    [Fact]
    public void EXECUTION_COMPLETED_sets_completedAt_and_status_COMPLETED()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = ReducerTestHelpers.CreateEvent(EventTypeConstants.ExecutionCompleted, "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.NotNull(result.CompletedAt);
        Assert.Equal(ExecutionStatus.COMPLETED, result.Status);
    }

    /// <summary>NODE_CREATED で payload の nodeId/nodeType の新規ノードが IDLE で追加されること。</summary>
    [Fact]
    public void NODE_CREATED_creates_IDLE_node()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeCreated,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("nodeType", "task")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.True(result.Nodes.ContainsKey("node-1"));
        Assert.Equal("node-1", result.Nodes["node-1"].NodeId);
        Assert.Equal("task", result.Nodes["node-1"].NodeType);
        Assert.Equal(NodeStatus.IDLE, result.Nodes["node-1"].Status);
        Assert.Equal(0, result.Nodes["node-1"].Attempt);
    }

    /// <summary>既存ノードに対して NODE_CREATED を送っても上書きされず既存の status が保たれること。</summary>
    [Fact]
    public void NODE_CREATED_does_not_overwrite_existing_node()
    {
        // Arrange: 既に READY の node-1 がいる状態
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.READY, 0),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeCreated,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("nodeType", "task")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.READY, result.Nodes["node-1"].Status);
    }

    /// <summary>NODE_READY 適用で指定ノードの status が READY に更新されること。</summary>
    [Fact]
    public void NODE_READY_updates_node_status_to_READY()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.IDLE, 0),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeReady,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.READY, result.Nodes["node-1"].Status);
    }

    /// <summary>存在しない nodeId に対する NODE_READY は No-op で state が変わらないこと。</summary>
    [Fact]
    public void NODE_READY_does_nothing_when_node_missing()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeReady,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "non-existent")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(state.Nodes.Count, result.Nodes.Count);
        Assert.False(result.Nodes.ContainsKey("non-existent"));
    }

    /// <summary>NODE_STARTED 適用で status が RUNNING になり、attempt と workerId がセットされること。</summary>
    [Fact]
    public void NODE_STARTED_sets_RUNNING_attempt_and_workerId()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.READY, 0),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeStarted,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("attempt", 1), ("workerId", "worker-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.RUNNING, result.Nodes["node-1"].Status);
        Assert.Equal(1, result.Nodes["node-1"].Attempt);
        Assert.Equal("worker-1", result.Nodes["node-1"].WorkerId);
    }

    /// <summary>NODE_STARTED で既存の attempt がイベントより大きい場合、max が維持されること。</summary>
    [Fact]
    public void NODE_STARTED_uses_max_attempt()
    {
        // Arrange: 既に attempt=3 のノード
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.READY, 3),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeStarted,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("attempt", 1)));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(3, result.Nodes["node-1"].Attempt);
    }

    /// <summary>NODE_WAITING 適用で status が WAITING になり waitKey がセットされること。</summary>
    [Fact]
    public void NODE_WAITING_sets_WAITING_and_waitKey()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeWaiting,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("waitKey", "wait-123")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.WAITING, result.Nodes["node-1"].Status);
        Assert.Equal("wait-123", result.Nodes["node-1"].WaitKey);
    }

    /// <summary>WAITING のノードに NODE_RESUMED を適用すると RUNNING に戻ること。</summary>
    [Fact]
    public void NODE_RESUMED_Updates_WAITING_to_RUNNING()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.WAITING, 1) with { WaitKey = "wait-123" },
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeResumed,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.RUNNING, result.Nodes["node-1"].Status);
    }

    /// <summary>WAITING 以外のノードに NODE_RESUMED を適用しても No-op であること。</summary>
    [Fact]
    public void NODE_RESUMED_is_noop_when_node_not_WAITING()
    {
        // Arrange: RUNNING のノード
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeResumed,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.RUNNING, result.Nodes["node-1"].Status);
    }

    /// <summary>NODE_SUCCEEDED 適用で status が SUCCEEDED になり output がセットされること。</summary>
    [Fact]
    public void NODE_SUCCEEDED_sets_SUCCEEDED_and_output()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var output = new Dictionary<string, object?> { ["result"] = "success" };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeSucceeded,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("output", output)));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.SUCCEEDED, result.Nodes["node-1"].Status);
        Assert.Equal(output, result.Nodes["node-1"].Output);
    }

    /// <summary>NODE_FAILED 適用で status が FAILED になり error がセットされること。</summary>
    [Fact]
    public void NODE_FAILED_sets_FAILED_and_error()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var error = new Dictionary<string, object?> { ["message"] = "error" };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeFailed,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("error", error)));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.FAILED, result.Nodes["node-1"].Status);
        Assert.Equal(error, result.Nodes["node-1"].Error);
    }

    /// <summary>NODE_CANCELED 適用でノードの status が CANCELED になること。</summary>
    [Fact]
    public void NODE_CANCELED_sets_node_status_CANCELED()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeCanceled,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.CANCELED, result.Nodes["node-1"].Status);
    }

    /// <summary>ステータス優先度により、既に FAILED の execution に EXECUTION_STARTED を適用してもダウングレードしないこと。</summary>
    [Fact]
    public void Status_priority_does_not_downgrade_execution_status()
    {
        // Arrange: 既に FAILED の状態
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with { Status = ExecutionStatus.FAILED };
        var envelope = ReducerTestHelpers.CreateEvent(EventTypeConstants.ExecutionStarted, "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(ExecutionStatus.FAILED, result.Status);
    }

    /// <summary>ステータス優先度により、既に SUCCEEDED のノードに NODE_READY を適用してもダウングレードしないこと。</summary>
    [Fact]
    public void Status_priority_does_not_downgrade_node_status()
    {
        // Arrange: 既に SUCCEEDED のノード
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.SUCCEEDED, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeReady,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.SUCCEEDED, result.Nodes["node-1"].Status);
    }

    /// <summary>Cancel 要求済みのとき、進行系イベント（NODE_READY）は適用されずノード状態が変わらないこと。</summary>
    [Fact]
    public void Cancel_request_ignores_progress_events()
    {
        // Arrange: cancelRequestedAt 済み、RUNNING ノード
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            CancelRequestedAt = "2024-01-01T00:00:00Z",
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeReady,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1")));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.RUNNING, result.Nodes["node-1"].Status);
    }

    /// <summary>Cancel 要求済みでも終端イベント（NODE_FAILED）は適用されノードが FAILED になること。</summary>
    [Fact]
    public void Cancel_request_allows_terminal_events()
    {
        // Arrange: cancelRequestedAt 済み、RUNNING ノード
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            CancelRequestedAt = "2024-01-01T00:00:00Z",
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeFailed,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("error", new Dictionary<string, object?>())));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(NodeStatus.FAILED, result.Nodes["node-1"].Status);
    }

    /// <summary>NODE_FAIL_REPORTED は error のみ更新し、status は変更しないこと。</summary>
    [Fact]
    public void NODE_FAIL_REPORTED_updates_error_without_changing_status()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1") with
        {
            Nodes = new Dictionary<string, NodeState>
            {
                ["node-1"] = new NodeState("node-1", "task", NodeStatus.RUNNING, 1),
            },
        };
        var error = new Dictionary<string, object?> { ["message"] = "error reported" };
        var envelope = ReducerTestHelpers.CreateEvent(
            EventTypeConstants.NodeFailReported,
            "exec-1",
            ReducerTestHelpers.Payload(("nodeId", "node-1"), ("error", error)));

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(error, result.Nodes["node-1"].Error);
        Assert.Equal(NodeStatus.RUNNING, result.Nodes["node-1"].Status);
    }

    /// <summary>未知のイベント type は適用されず state がそのままであること（監査用 No-op）。</summary>
    [Fact]
    public void Unknown_event_type_is_ignored()
    {
        // Arrange
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = ReducerTestHelpers.CreateEvent("UNKNOWN_EVENT_TYPE", "exec-1");

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(state.ExecutionId, result.ExecutionId);
        Assert.Equal(state.GraphId, result.GraphId);
        Assert.Equal(state.Status, result.Status);
        Assert.Equal(state.Nodes.Count, result.Nodes.Count);
    }

    /// <summary>サポート外の schemaVersion のイベントは適用されず state がそのままであること。</summary>
    [Fact]
    public void Different_schema_version_is_ignored()
    {
        // Arrange: schemaVersion=2 のイベント
        var state = ReducerTestHelpers.CreateInitialState("exec-1");
        var envelope = new EventEnvelope(
            Guid.NewGuid().ToString(),
            "exec-1",
            EventTypeConstants.ExecutionStarted,
            DateTime.UtcNow.ToString("O"),
            new Actor(ActorKind.System),
            SchemaVersion: 2,
            Payload: new Dictionary<string, object?>());

        // Act
        var result = ExecutionReducer.Reduce(state, envelope);

        // Assert
        Assert.Equal(state.Status, result.Status);
    }
}
