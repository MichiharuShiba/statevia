import { describe, expect, it } from "vitest";
import {
  parseExecutionStreamEvent,
  applyExecutionStreamEvent
} from "../../app/lib/executionStream";
import type { ExecutionDTO } from "../../app/lib/types";

const baseExecution: ExecutionDTO = {
  executionId: "ex-1",
  status: "ACTIVE",
  graphId: "g-1",
  cancelRequestedAt: null,
  canceledAt: null,
  failedAt: null,
  completedAt: null,
  nodes: [
    {
      nodeId: "n-1",
      nodeType: "TASK",
      status: "IDLE",
      attempt: 0,
      workerId: null,
      waitKey: null,
      canceledByExecution: false
    }
  ]
};

describe("parseExecutionStreamEvent", () => {
  it("GraphUpdated イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "RUNNING" }] },
      at: "2026-01-01T00:00:00Z"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("GraphUpdated");
    expect(result?.executionId).toBe("ex-1");
  });

  it("ExecutionStatusChanged イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "COMPLETED",
      at: "2026-01-01T00:00:00Z"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("ExecutionStatusChanged");
    expect((result as { to: string }).to).toBe("COMPLETED");
  });

  it("空 payload のとき null を返す", () => {
    // Arrange
    const raw = "";

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("不正な JSON のとき null を返す", () => {
    // Arrange
    const raw = "not json";

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("未対応の event type のとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "EXECUTION_CREATED",
      executionId: "ex-1"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("executionId が無いとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify({ type: "GraphUpdated", patch: { nodes: [] } });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("NodeCancelled イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "NodeCancelled",
      executionId: "ex-1",
      nodeId: "n-1",
      cancel: { reason: "user" },
      at: "2026-01-01T00:00:00Z"
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("NodeCancelled");
    expect((result as { nodeId: string }).nodeId).toBe("n-1");
  });

  it("NodeFailed イベントをパースする", () => {
    // Arrange
    const raw = JSON.stringify({
      type: "NodeFailed",
      executionId: "ex-1",
      nodeId: "n-1",
      error: { message: "timeout" }
    });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.type).toBe("NodeFailed");
  });

  it("type が無いとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify({ executionId: "ex-1" });

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("パース結果がオブジェクトでないとき null を返す", () => {
    // Arrange
    const raw = JSON.stringify("string");

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });
});

describe("applyExecutionStreamEvent", () => {
  it("GraphUpdated を適用してノードの status を更新する", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1", status: "RUNNING", attempt: 1 }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes[0].status).toBe("RUNNING");
    expect(next.nodes[0].attempt).toBe(1);
  });

  it("GraphUpdated を適用してノードの error と cancelReason をマージする", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: {
          nodes: [
            {
              nodeId: "n-1",
              status: "FAILED",
              error: { message: "merged error" },
              cancelReason: "merged reason"
            }
          ]
        }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes[0].status).toBe("FAILED");
    expect(next.nodes[0].error).toEqual({ message: "merged error" });
    expect(next.nodes[0].cancelReason).toBe("merged reason");
  });

  it("ExecutionStatusChanged を COMPLETED に適用する", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "ExecutionStatusChanged",
        executionId: "ex-1",
        to: "COMPLETED",
        at: "2026-01-01T00:00:00Z"
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.status).toBe("COMPLETED");
    expect(next.completedAt).toBe("2026-01-01T00:00:00Z");
  });

  it("event の executionId が一致しないとき現在状態をそのまま返す", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "ExecutionStatusChanged",
        executionId: "ex-other",
        to: "COMPLETED"
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next).toBe(baseExecution);
  });

  it("NodeFailed を適用してノード status を FAILED にする", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "NodeFailed",
        executionId: "ex-1",
        nodeId: "n-1",
        error: { message: "boom" }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes[0].status).toBe("FAILED");
    expect(next.nodes[0].error).toEqual({ message: "boom" });
  });

  it("NodeCancelled を適用して canceledByExecution を立てる", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "NodeCancelled",
        executionId: "ex-1",
        nodeId: "n-1",
        cancel: { reason: "user" }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes[0].status).toBe("CANCELED");
    expect(next.nodes[0].canceledByExecution).toBe(true);
    expect(next.nodes[0].cancelReason).toBe("user");
  });

  it("ExecutionStatusChanged を FAILED に適用する", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "ExecutionStatusChanged",
        executionId: "ex-1",
        to: "FAILED",
        at: "2026-01-01T12:00:00Z"
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.status).toBe("FAILED");
    expect(next.failedAt).toBe("2026-01-01T12:00:00Z");
  });

  it("ExecutionStatusChanged を CANCELED に適用する", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "ExecutionStatusChanged",
        executionId: "ex-1",
        to: "CANCELED",
        at: "2026-01-01T12:00:00Z"
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.status).toBe("CANCELED");
    expect(next.canceledAt).toBe("2026-01-01T12:00:00Z");
  });

  it("to が未知の status のとき ACTIVE に正規化する", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "ExecutionStatusChanged",
        executionId: "ex-1",
        to: "PENDING",
        at: "2026-01-01T12:00:00Z"
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.status).toBe("ACTIVE");
  });

  it("to が CANCELLED（英国綴り）のとき CANCELED に正規化する", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "ExecutionStatusChanged",
        executionId: "ex-1",
        to: "CANCELLED",
        at: "2026-01-01T12:00:00Z"
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.status).toBe("CANCELED");
    expect(next.canceledAt).toBe("2026-01-01T12:00:00Z");
  });

  it("GraphUpdated を適用し存在しないノードを追加する", () => {
    // Arrange
    const execWithOneNode = { ...baseExecution, nodes: [...baseExecution.nodes] };
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-2", status: "IDLE" }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(execWithOneNode, event);

    // Assert
    expect(next.nodes).toHaveLength(2);
    const added = next.nodes.find((n) => n.nodeId === "n-2");
    expect(added?.status).toBe("IDLE");
    expect(added?.nodeType).toBe("Unknown");
  });

  it("applies GraphUpdated with empty patch nodes leaves nodes unchanged (境界値: 空配列)", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes).toEqual(baseExecution.nodes);
  });

  it("境界値: 空白のみの payload は null", () => {
    // Arrange
    const raw = "   ";

    // Act
    const result = parseExecutionStreamEvent(raw);

    // Assert
    expect(result).toBeNull();
  });

  it("境界値: GraphUpdated で patch.nodes が undefined の場合は既存 nodes を維持", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: {}
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes).toEqual(baseExecution.nodes);
  });

  it("GraphUpdated で status が CANCELLED のとき normalizeNodeStatus で CANCELED に正規化される", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1", status: "CANCELLED" }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes[0].status).toBe("CANCELED");
  });

  it("GraphUpdated で patch に status を省略したときは patchNode.status が falsy で null が渡る (L55)", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1" }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes).toHaveLength(1);
    expect(next.nodes[0].nodeId).toBe("n-1");
  });

  it("GraphUpdated で patch の status が不正な文字列のときは normalizeNodeStatus が null で status は undefined で上書きされる", () => {
    // Arrange
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1", status: "INVALID_STATUS" }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(baseExecution, event);

    // Assert
    expect(next.nodes[0].nodeId).toBe("n-1");
    expect(next.nodes).toHaveLength(1);
  });

  it("GraphUpdated で新規ノードに nodeType を省略すると Unknown になる", () => {
    // Arrange
    const emptyExecution = { ...baseExecution, nodes: [] };
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-new", status: "IDLE" }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(emptyExecution, event);

    // Assert
    expect(next.nodes).toHaveLength(1);
    expect(next.nodes[0].nodeType).toBe("Unknown");
  });

  it("境界値: execution の nodes が空のときに GraphUpdated で 1 件追加", () => {
    // Arrange
    const emptyExecution = { ...baseExecution, nodes: [] };
    const event = parseExecutionStreamEvent(
      JSON.stringify({
        type: "GraphUpdated",
        executionId: "ex-1",
        patch: { nodes: [{ nodeId: "n-1", status: "IDLE" }] }
      })
    )!;

    // Act
    const next = applyExecutionStreamEvent(emptyExecution, event);

    // Assert
    expect(next.nodes).toHaveLength(1);
    expect(next.nodes[0].nodeId).toBe("n-1");
    expect(next.nodes[0].status).toBe("IDLE");
  });
});
