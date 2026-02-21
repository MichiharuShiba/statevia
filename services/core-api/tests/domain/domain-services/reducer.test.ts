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
    it("should set graphId and status to ACTIVE", () => {
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
    it("should set status to ACTIVE", () => {
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
    it("should set cancelRequestedAt on first request", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("EXECUTION_CANCEL_REQUESTED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.cancelRequestedAt).toBe(event.occurredAt);
    });

    it("should not overwrite existing cancelRequestedAt", () => {
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
    it("should set canceledAt and status to CANCELED", () => {
      // Arrange
      const state = createInitialState("exec-1");
      const event = createEvent("EXECUTION_CANCELED", "exec-1");

      // Act
      const result = reduce(state, event);

      // Assert
      expect(result.canceledAt).toBe(event.occurredAt);
      expect(result.status).toBe("CANCELED");
    });

    it("should cancel all active nodes when execution is canceled", () => {
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
    it("should set failedAt and status to FAILED", () => {
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
    it("should set completedAt and status to COMPLETED", () => {
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
    it("should create a new node with IDLE status", () => {
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

    it("should not create duplicate nodes", () => {
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
    it("should update node status to READY", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should update node status to RUNNING and set attempt", () => {
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

    it("should use max attempt if node already has higher attempt", () => {
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

    it("should preserve existing workerId if workerId not provided", () => {
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

    it("should handle attempt with default value when attempt is not provided", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should update node status to WAITING and set waitKey", () => {
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

    it("should preserve existing waitKey if waitKey not provided", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should update node status from WAITING to RUNNING", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should update node status to SUCCEEDED and set output", () => {
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

    it("should preserve existing output if output not provided", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should update node status to FAILED and set error", () => {
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

    it("should preserve existing error if error not provided", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should update node status to CANCELED", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should not downgrade execution status", () => {
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

    it("should not downgrade node status", () => {
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
    it("should ignore progress events after cancel requested", () => {
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

    it("should allow terminal events after cancel requested", () => {
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
    it("should update node error without changing status", () => {
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

    it("should preserve existing error if error not provided", () => {
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

    it("should do nothing if node does not exist", () => {
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
    it("should ignore unknown event types (audit-only events)", () => {
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
    it("should ignore events with wrong schema version", () => {
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
      it("should handle large attempt value (1000)", () => {
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

      it("should handle very large attempt value (10000)", () => {
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

      it("should preserve max attempt when event has smaller value", () => {
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

      it("should handle attempt near Number.MAX_SAFE_INTEGER", () => {
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
      it("should handle execution with 100 nodes", () => {
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

      it("should handle execution with 1000 nodes", () => {
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

      it("should cancel all active nodes when execution is canceled with many nodes", () => {
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
      it("should handle large version value", () => {
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

      it("should handle version near Number.MAX_SAFE_INTEGER", () => {
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
      it("should handle very long executionId", () => {
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

      it("should handle very long nodeId", () => {
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

      it("should handle very long graphId", () => {
        // Arrange
        const longGraphId = "graph-" + "c".repeat(1000);
        const state = createInitialState("exec-1", "");
        const event = createEvent("EXECUTION_CREATED", "exec-1", { graphId: longGraphId });

        // Act
        const result = reduce(state, event);

        // Assert
        expect(result.graphId).toBe(longGraphId);
      });

      it("should handle very long workerId", () => {
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

      it("should handle very long waitKey", () => {
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
      it("should handle applying 100 events sequentially", () => {
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

      it("should handle applying 1000 events sequentially", () => {
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

      it("should handle complex event sequence with many nodes", () => {
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
      it("should handle attempt value of 0", () => {
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

      it("should default attempt to 1 when attempt is not provided", () => {
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

      it("should handle negative attempt value", () => {
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
