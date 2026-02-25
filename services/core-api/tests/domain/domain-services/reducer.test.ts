/**
 * Reducer のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect } from "vitest";
import { reduce } from "../../../src/domain/domain-services/reducer.js";
import { ExecutionState } from "../../../src/domain/value-objects/execution-state.js";
import { EventEnvelope } from "../../../src/domain/value-objects/event-envelope.js";
import { Actor } from "../../../src/domain/value-objects/actor.js";

const systemActor: Actor = { kind: "system" };
const userActor: Actor = { kind: "user", id: "user-1" };

function createEvent(
  type: string,
  executionId: string,
  payload: any = {},
  actor: Actor = systemActor
): EventEnvelope {
  return {
    eventId: crypto.randomUUID(),
    executionId,
    type,
    occurredAt: new Date().toISOString(),
    actor,
    schemaVersion: 1,
    payload
  };
}

function createInitialState(executionId: string, graphId: string = "graph-1"): ExecutionState {
  return {
    executionId,
    graphId,
    status: "ACTIVE",
    version: 0,
    nodes: {}
  };
}

describe("Reducer", () => {
  describe("EXECUTION_CREATED", () => {
    it("graphId と status を ACTIVE にセットする", () => {
      // Arrange
      const state = createInitialState("exec-1", "");
      const event = createEvent("EXECUTION_CREATED", "exec-1", { graphId: "graph-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.graphId).toBe("graph-1");
      expect(result.status).toBe("ACTIVE");
    });
  });

  describe("EXECUTION_STARTED", () => {
    it("status を ACTIVE にセットする", () => {
      // Arrange
      const state: ExecutionState = { ...createInitialState("exec-1"), status: "ACTIVE" as const };
      const event = createEvent("EXECUTION_STARTED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.status).toBe("ACTIVE");
    });
  });

  describe("EXECUTION_CANCEL_REQUESTED", () => {
    it("初回要求で cancelRequestedAt をセットする", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("EXECUTION_CANCEL_REQUESTED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.cancelRequestedAt).toBe(event.occurredAt);
    });

    it("既存の cancelRequestedAt は上書きしない", () => {
      // Arrange
      const existingTime = "2024-01-01T00:00:00Z";
      const state = { ...createInitialState("exec-1"), cancelRequestedAt: existingTime };
      const event = createEvent("EXECUTION_CANCEL_REQUESTED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.cancelRequestedAt).toBe(existingTime);
    });
  });

  describe("EXECUTION_CANCELED", () => {
    it("canceledAt と status を CANCELED にセットする", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("EXECUTION_CANCELED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.canceledAt).toBe(event.occurredAt);
      expect(result.status).toBe("CANCELED");
    });

    it("execution キャンセル時に全アクティブノードをキャンセルする", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "IDLE", attempt: 0 },
          "node-2": { nodeId: "node-2", nodeType: "task", status: "READY", attempt: 0 },
          "node-3": { nodeId: "node-3", nodeType: "task", status: "RUNNING", attempt: 1 },
          "node-4": { nodeId: "node-4", nodeType: "task", status: "WAITING", attempt: 1 },
          "node-5": { nodeId: "node-5", nodeType: "task", status: "SUCCEEDED", attempt: 1 }
        }
      };
      const event = createEvent("EXECUTION_CANCELED", "exec-1");
      
      const result = reduce(state, event);
      
      expect(result.status).toBe("CANCELED");
      expect(result.nodes["node-1"].status).toBe("CANCELED");
      expect(result.nodes["node-1"].canceledByExecution).toBe(true);
      expect(result.nodes["node-2"].status).toBe("CANCELED");
      expect(result.nodes["node-2"].canceledByExecution).toBe(true);
      expect(result.nodes["node-3"].status).toBe("CANCELED");
      expect(result.nodes["node-3"].canceledByExecution).toBe(true);
      expect(result.nodes["node-4"].status).toBe("CANCELED");
      expect(result.nodes["node-4"].canceledByExecution).toBe(true);
      expect(result.nodes["node-5"].status).toBe("SUCCEEDED"); // Already terminal
    });
  });

  describe("EXECUTION_FAILED", () => {
    it("failedAt と status を FAILED にセットする", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("EXECUTION_FAILED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.failedAt).toBe(event.occurredAt);
      expect(result.status).toBe("FAILED");
    });
  });

  describe("EXECUTION_COMPLETED", () => {
    it("completedAt と status を COMPLETED にセットする", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("EXECUTION_COMPLETED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.completedAt).toBe(event.occurredAt);
      expect(result.status).toBe("COMPLETED");
    });
  });

  describe("NODE_CREATED", () => {
    it("IDLE 状態の新規ノードを作成する", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_CREATED", "exec-1", { nodeId: "node-1", nodeType: "task" });
      
      const result = reduce(state, event);
      
      expect(result.nodes["node-1"]).toBeDefined();
      expect(result.nodes["node-1"].nodeId).toBe("node-1");
      expect(result.nodes["node-1"].nodeType).toBe("task");
      expect(result.nodes["node-1"].status).toBe("IDLE");
      expect(result.nodes["node-1"].attempt).toBe(0);
    });

    it("重複ノードは作成しない", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
        }
      };
      const event = createEvent("NODE_CREATED", "exec-1", { nodeId: "node-1", nodeType: "task" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("READY"); // Unchanged
    });
  });

  describe("NODE_READY", () => {
    it("ノード status を READY に更新する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "IDLE", attempt: 0 }
        }
      };
      const event = createEvent("NODE_READY", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("READY");
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_READY", "exec-1", { nodeId: "non-existent" });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("NODE_STARTED", () => {
    it("ノード status を RUNNING にし attempt をセットする", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
        }
      };
      const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 1, workerId: "worker-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("RUNNING");
      expect(result.nodes["node-1"].attempt).toBe(1);
      expect(result.nodes["node-1"].workerId).toBe("worker-1");
    });

    it("ノードの attempt が大きいときは max を採用する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 3 }
        }
      };
      const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 1 });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].attempt).toBe(3); // Max preserved
    });

    it("workerId が無いとき既存 workerId を維持する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0, workerId: "existing-worker" }
        }
      };
      const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 1 });
      
      const result = reduce(state, event);
      
      expect(result.nodes["node-1"].workerId).toBe("existing-worker");
    });

    it("attempt が無いときデフォルト値で扱う", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
        }
      };
      const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].attempt).toBe(1); // Default attempt value
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "non-existent", attempt: 1 });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("NODE_WAITING", () => {
    it("ノード status を WAITING にし waitKey をセットする", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_WAITING", "exec-1", { nodeId: "node-1", waitKey: "wait-123" });
      
      const result = reduce(state, event);
      
      expect(result.nodes["node-1"].status).toBe("WAITING");
      expect(result.nodes["node-1"].waitKey).toBe("wait-123");
    });

    it("waitKey が無いとき既存 waitKey を維持する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1, waitKey: "existing-wait" }
        }
      };
      const event = createEvent("NODE_WAITING", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].waitKey).toBe("existing-wait");
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_WAITING", "exec-1", { nodeId: "non-existent", waitKey: "wait-123" });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("NODE_RESUMED", () => {
    it("ノード status を WAITING から RUNNING に更新する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "WAITING", attempt: 1, waitKey: "wait-123" }
        }
      };
      const event = createEvent("NODE_RESUMED", "exec-1", { nodeId: "node-1" });
      
      const result = reduce(state, event);
      
      expect(result.nodes["node-1"].status).toBe("RUNNING");
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_RESUMED", "exec-1", { nodeId: "non-existent" });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("NODE_SUCCEEDED", () => {
    it("ノード status を SUCCEEDED にし output をセットする", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_SUCCEEDED", "exec-1", { nodeId: "node-1", output: { result: "success" } });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("SUCCEEDED");
      expect(result.nodes["node-1"].output).toEqual({ result: "success" });
    });

    it("output が無いとき既存 output を維持する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1, output: { existing: "data" } }
        }
      };
      const event = createEvent("NODE_SUCCEEDED", "exec-1", { nodeId: "node-1" });
      
      const result = reduce(state, event);
      
      expect(result.nodes["node-1"].output).toEqual({ existing: "data" });
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_SUCCEEDED", "exec-1", { nodeId: "non-existent", output: { result: "success" } });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("NODE_FAILED", () => {
    it("ノード status を FAILED にし error をセットする", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_FAILED", "exec-1", { nodeId: "node-1", error: { message: "error" } });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("FAILED");
      expect(result.nodes["node-1"].error).toEqual({ message: "error" });
    });

    it("error が無いとき既存 error を維持する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1, error: { existing: "error" } }
        }
      };
      const event = createEvent("NODE_FAILED", "exec-1", { nodeId: "node-1" });
      
      const result = reduce(state, event);
      
      expect(result.nodes["node-1"].error).toEqual({ existing: "error" });
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_FAILED", "exec-1", { nodeId: "non-existent", error: { message: "error" } });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("NODE_CANCELED", () => {
    it("ノード status を CANCELED に更新する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_CANCELED", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("CANCELED");
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_CANCELED", "exec-1", { nodeId: "non-existent" });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
      expect(result.nodes["non-existent"]).toBeUndefined();
    });
  });

  describe("Status priority", () => {
    it("execution status をダウングレードしない", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        status: "FAILED" as const
      };
      const event = createEvent("EXECUTION_STARTED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.status).toBe("FAILED"); // Should not downgrade
    });

    it("ノード status をダウングレードしない", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "SUCCEEDED", attempt: 1 }
        }
      };
      const event = createEvent("NODE_READY", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("SUCCEEDED"); // Should not downgrade
    });
  });

  describe("Cancel request handling", () => {
    it("cancel 要求後は進行イベントを無視する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        cancelRequestedAt: "2024-01-01T00:00:00Z",
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_READY", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("RUNNING"); // Unchanged
    });

    it("cancel 要求後も終端イベントは許可する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        cancelRequestedAt: "2024-01-01T00:00:00Z",
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_FAILED", "exec-1", { nodeId: "node-1", error: {} });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].status).toBe("FAILED"); // Terminal event allowed
    });
  });

  describe("NODE_FAIL_REPORTED", () => {
    it("ノードの error を更新し status は変えない", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      const event = createEvent("NODE_FAIL_REPORTED", "exec-1", { nodeId: "node-1", error: { message: "error reported" } });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].error).toEqual({ message: "error reported" });
      expect(result.nodes["node-1"].status).toBe("RUNNING"); // Status unchanged
    });

    it("error が無いとき既存 error を維持する", () => {
      // Arrange
      const state: ExecutionState = {
        ...createInitialState("exec-1"),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1, error: { message: "existing error" } }
        }
      };
      const event = createEvent("NODE_FAIL_REPORTED", "exec-1", { nodeId: "node-1" });

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.nodes["node-1"].error).toEqual({ message: "existing error" });
    });

    it("ノードが存在しないときは何もしない", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("NODE_FAIL_REPORTED", "exec-1", { nodeId: "non-existent" });
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state);
    });
  });

  describe("Unknown event types", () => {
    it("未知のイベント種別は無視する（監査用のみ）", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event: EventEnvelope = {
        ...createEvent("UNKNOWN_EVENT_TYPE", "exec-1"),
        type: "UNKNOWN_EVENT_TYPE" as any
      };
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state); // Unchanged
    });
  });

  describe("Schema version", () => {
    it("スキーマバージョンが異なるイベントは無視する", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event: EventEnvelope = {
        ...createEvent("EXECUTION_STARTED", "exec-1"),
        schemaVersion: 2 as any
      };
      
      // Act
      const result = reduce(state, event);

      // Assert
      expect(result).toEqual(state); // Unchanged
    });
  });

  describe("Boundary values and edge cases", () => {
    describe("Large attempt values", () => {
      it("大きな attempt (1000) を扱う", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 1000 });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].attempt).toBe(1000);
        expect(result.nodes["node-1"].status).toBe("RUNNING");
      });

      it("非常に大きな attempt (10000) を扱う", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 10000 });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].attempt).toBe(10000);
        expect(result.nodes["node-1"].status).toBe("RUNNING");
      });

      it("イベントの attempt が小さいときは max を維持する", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 10000 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 1 });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].attempt).toBe(10000); // Max preserved
      });

      it("Number.MAX_SAFE_INTEGER に近い attempt を扱う", () => {
        // Arrange
        const largeAttempt = Number.MAX_SAFE_INTEGER - 1;
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: largeAttempt });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].attempt).toBe(largeAttempt);
        expect(result.nodes["node-1"].status).toBe("RUNNING");
      });
    });

    describe("Many nodes", () => {
      it("100 ノードの execution を扱う", () => {
        // Arrange
        const nodes: Record<string, any> = {};
        for (let i = 1; i <= 100; i++) {
          nodes[`node-${i}`] = {
            nodeId: `node-${i}`,
            nodeType: "task",
            status: i % 2 === 0 ? "READY" : "IDLE",
            attempt: 0
          };
        }
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes
        };
        const event = createEvent("NODE_READY", "exec-1", { nodeId: "node-1" });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(Object.keys(result.nodes)).toHaveLength(100);
        expect(result.nodes["node-1"].status).toBe("READY");
        expect(result.nodes["node-50"].status).toBe("READY"); // Unchanged
      });

      it("1000 ノードの execution を扱う", () => {
        // Arrange
        const nodes: Record<string, any> = {};
        for (let i = 1; i <= 1000; i++) {
          nodes[`node-${i}`] = {
            nodeId: `node-${i}`,
            nodeType: "task",
            status: "IDLE",
            attempt: 0
          };
        }
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes
        };
        const event = createEvent("NODE_READY", "exec-1", { nodeId: "node-500" });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(Object.keys(result.nodes)).toHaveLength(1000);
        expect(result.nodes["node-500"].status).toBe("READY");
        expect(result.nodes["node-1"].status).toBe("IDLE"); // Unchanged
      });

      it("多数ノード時に execution キャンセルで全アクティブノードをキャンセルする", () => {
        // Arrange
        const nodes: Record<string, any> = {};
        for (let i = 1; i <= 100; i++) {
          const statuses = ["IDLE", "READY", "RUNNING", "WAITING", "SUCCEEDED"];
          nodes[`node-${i}`] = {
            nodeId: `node-${i}`,
            nodeType: "task",
            status: statuses[(i - 1) % statuses.length], // Fix: use (i-1) to get correct status mapping
            attempt: 0
          };
        }
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes
        };
        const event = createEvent("EXECUTION_CANCELED", "exec-1");

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.status).toBe("CANCELED");
        // Active nodes should be canceled
        expect(result.nodes["node-1"].status).toBe("CANCELED"); // IDLE -> CANCELED
        expect(result.nodes["node-2"].status).toBe("CANCELED"); // READY -> CANCELED
        expect(result.nodes["node-3"].status).toBe("CANCELED"); // RUNNING -> CANCELED
        expect(result.nodes["node-4"].status).toBe("CANCELED"); // WAITING -> CANCELED
        expect(result.nodes["node-5"].status).toBe("SUCCEEDED"); // Already terminal
      });
    });

    describe("Large version values", () => {
      it("大きな version を扱う", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          version: 1000000
        };
        const event = createEvent("EXECUTION_STARTED", "exec-1");

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.version).toBe(1000000); // Version is preserved
        expect(result.status).toBe("ACTIVE");
      });

      it("Number.MAX_SAFE_INTEGER に近い version を扱う", () => {
        // Arrange
        const largeVersion = Number.MAX_SAFE_INTEGER - 1;
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          version: largeVersion
        };
        const event = createEvent("EXECUTION_STARTED", "exec-1");

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.version).toBe(largeVersion);
      });
    });

    describe("Long string values", () => {
      it("非常に長い executionId を扱う", () => {
        // Arrange
        const longExecutionId = "exec-" + "a".repeat(1000);
        const state = createInitialState(longExecutionId);
        const event = createEvent("EXECUTION_STARTED", longExecutionId);

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.executionId).toBe(longExecutionId);
        expect(result.status).toBe("ACTIVE");
      });

      it("非常に長い nodeId を扱う", () => {
        // Arrange
        const longNodeId = "node-" + "b".repeat(1000);
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            [longNodeId]: { nodeId: longNodeId, nodeType: "task", status: "IDLE", attempt: 0 }
          }
        };
        const event = createEvent("NODE_READY", "exec-1", { nodeId: longNodeId });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes[longNodeId]).toBeDefined();
        expect(result.nodes[longNodeId].status).toBe("READY");
      });

      it("非常に長い graphId を扱う", () => {
        // Arrange
        const longGraphId = "graph-" + "c".repeat(1000);
        const state = createInitialState("exec-1", "");
        const event = createEvent("EXECUTION_CREATED", "exec-1", { graphId: longGraphId });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.graphId).toBe(longGraphId);
      });

      it("非常に長い workerId を扱う", () => {
        // Arrange
        const longWorkerId = "worker-" + "d".repeat(1000);
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 1, workerId: longWorkerId });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].workerId).toBe(longWorkerId);
      });

      it("非常に長い waitKey を扱う", () => {
        // Arrange
        const longWaitKey = "wait-" + "e".repeat(1000);
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
          }
        };
        const event = createEvent("NODE_WAITING", "exec-1", { nodeId: "node-1", waitKey: longWaitKey });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].waitKey).toBe(longWaitKey);
      });
    });

    describe("Many events", () => {
      it("100 イベントを順に適用する", () => {
        // Arrange
        let state = createInitialState("exec-1");
        const events: EventEnvelope[] = [];

        // Create 100 nodes
        for (let i = 1; i <= 100; i++) {
          events.push(createEvent("NODE_CREATED", "exec-1", { nodeId: `node-${i}`, nodeType: "task" }));
        }

        // Act
        for (const event of events) {
          state = reduce(state, event);
        }

        // Assert
        expect(Object.keys(state.nodes)).toHaveLength(100);
        for (let i = 1; i <= 100; i++) {
          expect(state.nodes[`node-${i}`]).toBeDefined();
          expect(state.nodes[`node-${i}`].status).toBe("IDLE");
        }
      });

      it("1000 イベントを順に適用する", () => {
        // Arrange
        let state = createInitialState("exec-1");
        const events: EventEnvelope[] = [];

        // Create 1000 nodes
        for (let i = 1; i <= 1000; i++) {
          events.push(createEvent("NODE_CREATED", "exec-1", { nodeId: `node-${i}`, nodeType: "task" }));
        }

        // Act
        for (const event of events) {
          state = reduce(state, event);
        }

        // Assert
        expect(Object.keys(state.nodes)).toHaveLength(1000);
        expect(state.nodes["node-1"]).toBeDefined();
        expect(state.nodes["node-500"]).toBeDefined();
        expect(state.nodes["node-1000"]).toBeDefined();
      });

      it("多数ノードの複雑なイベント列を扱う", () => {
        // Arrange
        let state = createInitialState("exec-1");
        const events: EventEnvelope[] = [];

        // Create 50 nodes and transition them through states
        for (let i = 1; i <= 50; i++) {
          events.push(createEvent("NODE_CREATED", "exec-1", { nodeId: `node-${i}`, nodeType: "task" }));
          events.push(createEvent("NODE_READY", "exec-1", { nodeId: `node-${i}` }));
          events.push(createEvent("NODE_STARTED", "exec-1", { nodeId: `node-${i}`, attempt: 1 }));
          if (i % 2 === 0) {
            events.push(createEvent("NODE_SUCCEEDED", "exec-1", { nodeId: `node-${i}`, output: { result: `result-${i}` } }));
          }
        }

        // Act
        for (const event of events) {
          state = reduce(state, event);
        }

        // Assert
        expect(Object.keys(state.nodes)).toHaveLength(50);
        // Even nodes should be SUCCEEDED
        expect(state.nodes["node-2"].status).toBe("SUCCEEDED");
        expect(state.nodes["node-2"].output).toEqual({ result: "result-2" });
        // Odd nodes should be RUNNING
        expect(state.nodes["node-1"].status).toBe("RUNNING");
        expect(state.nodes["node-3"].status).toBe("RUNNING");
      });
    });

    describe("Edge cases with Math.max", () => {
      it("attempt 0 を扱う", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: 0 });

        // Act
        const result = reduce(state, event);

        // Assert
        // Math.max(0, 0) = 0, but p.attempt ?? 1 means if attempt is undefined/null, use 1
        // Since attempt: 0 is explicitly provided, it's 0 (not undefined), so Math.max(0, 0) = 0
        expect(result.nodes["node-1"].attempt).toBe(0);
        expect(result.nodes["node-1"].status).toBe("RUNNING");
      });

      it("attempt が無いときデフォルト 1 にする", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1" }); // attempt not provided

        // Act
        const result = reduce(state, event);

        // Assert
        // When attempt is undefined, p.attempt ?? 1 = 1, so Math.max(0, 1) = 1
        expect(result.nodes["node-1"].attempt).toBe(1);
      });

      it("負の attempt を扱う（max で正になる）", () => {
        // Arrange
        const state: ExecutionState = {
          ...createInitialState("exec-1"),
          nodes: {
            "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 5 }
          }
        };
        const event = createEvent("NODE_STARTED", "exec-1", { nodeId: "node-1", attempt: -1 });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.nodes["node-1"].attempt).toBe(5); // Max preserved (5 > -1)
      });
    });
  });
});

declare const crypto: { randomUUID(): string };
