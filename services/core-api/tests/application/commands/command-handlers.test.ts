/**
 * Command Handlers のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect } from "vitest";
import {
  cmdCreateExecution,
  cmdStartExecution,
  cmdCancelExecution,
  cmdPutNodeWaiting,
  cmdResumeNode,
  cmdCreateNode,
  cmdStartNode,
  applyEvents
} from "../../../src/application/commands/command-handlers.js";
import { ExecutionState } from "../../../src/domain/value-objects/execution-state.js";
import { Actor } from "../../../src/domain/value-objects/actor.js";
import { DomainError } from "../../../src/domain/errors.js";

const systemActor: Actor = { kind: "system" };
const userActor: Actor = { kind: "user", id: "user-1" };

function createExecutionState(overrides: Partial<ExecutionState> = {}): ExecutionState {
  return {
    executionId: "exec-1",
    graphId: "graph-1",
    status: "ACTIVE",
    version: 0,
    nodes: {},
    ...overrides
  };
}

describe("Command Handlers", () => {
  describe("cmdCreateExecution", () => {
    it("should create EXECUTION_CREATED event", () => {
      // Arrange
      const args = {
        executionId: "exec-1",
        graphId: "graph-1",
        actor: systemActor,
        correlationId: "corr-1"
      };

      // Act
      const result = cmdCreateExecution(args);

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("EXECUTION_CREATED");
      expect(result.events[0].executionId).toBe("exec-1");
      expect(result.events[0].payload).toHaveProperty("graphId", "graph-1");
      expect(result.events[0].correlationId).toBe("corr-1");
    });

    it("should include input in payload if provided", () => {
      // Arrange
      const args = {
        executionId: "exec-1",
        graphId: "graph-1",
        input: { key: "value" },
        actor: systemActor
      };

      // Act
      const result = cmdCreateExecution(args);

      // Assert
      expect(result.events[0].payload).toHaveProperty("input", { key: "value" });
    });
  });

  describe("cmdStartExecution", () => {
    it("should create EXECUTION_STARTED event for active execution", () => {
      // Arrange
      const state = createExecutionState({ status: "ACTIVE" });

      // Act
      const result = cmdStartExecution(state, systemActor, "corr-1");

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("EXECUTION_STARTED");
      expect(result.events[0].executionId).toBe("exec-1");
    });

    it("should throw for terminal execution", () => {
      // Arrange
      const state = createExecutionState({ status: "COMPLETED" });

      // Act & Assert
      expect(() => cmdStartExecution(state, systemActor)).toThrow(DomainError);
    });

    it("should throw when cancel requested", () => {
      // Arrange
      const state = createExecutionState({
        status: "ACTIVE",
        cancelRequestedAt: "2024-01-01T00:00:00Z"
      });

      // Act & Assert
      expect(() => cmdStartExecution(state, systemActor)).toThrow(DomainError);
    });
  });

  describe("cmdCancelExecution", () => {
    it("should create EXECUTION_CANCEL_REQUESTED event", () => {
      // Arrange
      const state = createExecutionState({ status: "ACTIVE" });

      // Act
      const result = cmdCancelExecution(state, systemActor, "reason", "corr-1");

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("EXECUTION_CANCEL_REQUESTED");
      expect(result.events[0].payload).toHaveProperty("reason", "reason");
      expect(result.events[0].correlationId).toBe("corr-1");
    });

    it("should handle missing reason", () => {
      // Arrange
      const state = createExecutionState({ status: "ACTIVE" });

      // Act
      const result = cmdCancelExecution(state, systemActor);

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].payload).toHaveProperty("reason", null);
    });

    it("should be idempotent for terminal executions", () => {
      // Arrange
      const state = createExecutionState({ status: "COMPLETED" });

      // Act
      const result = cmdCancelExecution(state, systemActor);

      // Assert
      expect(result.events).toHaveLength(0);
    });

    it("should be idempotent for FAILED status", () => {
      // Arrange
      const state = createExecutionState({ status: "FAILED" });

      // Act
      const result = cmdCancelExecution(state, systemActor);

      // Assert
      expect(result.events).toHaveLength(0);
    });

    it("should be idempotent for CANCELED status", () => {
      // Arrange
      const state = createExecutionState({ status: "CANCELED" });

      // Act
      const result = cmdCancelExecution(state, systemActor);

      // Assert
      expect(result.events).toHaveLength(0);
    });

    it("should not create duplicate cancel request", () => {
      // Arrange
      const state = createExecutionState({
        status: "ACTIVE",
        cancelRequestedAt: "2024-01-01T00:00:00Z"
      });

      // Act
      const result = cmdCancelExecution(state, systemActor);

      // Assert
      expect(result.events).toHaveLength(0);
    });
  });

  describe("cmdPutNodeWaiting", () => {
    it("should create NODE_WAITING event for running node", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };

      // Act
      const result = cmdPutNodeWaiting(state, systemActor, "node-1", "wait-123", { prompt: "test" });

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("NODE_WAITING");
      expect(result.events[0].payload).toHaveProperty("nodeId", "node-1");
      expect(result.events[0].payload).toHaveProperty("waitKey", "wait-123");
      expect(result.events[0].payload).toHaveProperty("prompt", { prompt: "test" });
    });

    it("should set prompt to null if prompt not provided", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };

      // Act
      const result = cmdPutNodeWaiting(state, systemActor, "node-1", "wait-123");

      // Assert
      expect(result.events[0].payload).toHaveProperty("prompt", null);
    });

    it("should set waitKey to null if waitKey not provided", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };

      // Act
      const result = cmdPutNodeWaiting(state, systemActor, "node-1");

      // Assert
      expect(result.events[0].payload).toHaveProperty("waitKey", null);
      expect(result.events[0].payload).toHaveProperty("prompt", null);
    });

    it("should include correlationId in event", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };

      // Act
      const result = cmdPutNodeWaiting(state, systemActor, "node-1", "wait-123", undefined, "corr-1");

      // Assert
      expect(result.events[0].correlationId).toBe("corr-1");
    });

    it("should throw for non-running node", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
        }
      };

      // Act & Assert
      expect(() => cmdPutNodeWaiting(state, systemActor, "node-1")).toThrow(DomainError);
    });

    it("should throw when execution is terminal", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState({ status: "COMPLETED" }),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };

      // Act & Assert
      expect(() => cmdPutNodeWaiting(state, systemActor, "node-1")).toThrow(DomainError);
    });
  });

  describe("cmdResumeNode", () => {
    it("should create NODE_RESUMED event for waiting node", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "WAITING", attempt: 1, waitKey: "wait-123" }
        }
      };

      // Act
      const result = cmdResumeNode(state, systemActor, "node-1", "resume-123");

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("NODE_RESUMED");
      expect(result.events[0].payload).toHaveProperty("nodeId", "node-1");
      expect(result.events[0].payload).toHaveProperty("resumeKey", "resume-123");
    });

    it("should include correlationId in event", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "WAITING", attempt: 1, waitKey: "wait-123" }
        }
      };
      const result = cmdResumeNode(state, systemActor, "node-1", "resume-123", "corr-1");

      expect(result.events[0].correlationId).toBe("corr-1");
    });

    it("should handle missing resumeKey", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "WAITING", attempt: 1, waitKey: "wait-123" }
        }
      };
      const result = cmdResumeNode(state, systemActor, "node-1");

      expect(result.events).toHaveLength(1);
      expect(result.events[0].payload).toHaveProperty("resumeKey", null);
    });

    it("should throw for non-waiting node", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      
      expect(() => cmdResumeNode(state, systemActor, "node-1")).toThrow(DomainError);
    });

    it("should throw when execution is terminal", () => {
      const state: ExecutionState = {
        ...createExecutionState({ status: "COMPLETED" }),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "WAITING", attempt: 1 }
        }
      };
      
      expect(() => cmdResumeNode(state, systemActor, "node-1")).toThrow(DomainError);
    });

    it("should throw when cancel requested", () => {
      const state: ExecutionState = {
        ...createExecutionState({
          status: "ACTIVE",
          cancelRequestedAt: "2024-01-01T00:00:00Z"
        }),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "WAITING", attempt: 1 }
        }
      };
      
      expect(() => cmdResumeNode(state, systemActor, "node-1")).toThrow(DomainError);
    });
  });

  describe("cmdCreateNode", () => {
    it("should create NODE_CREATED event", () => {
      // Arrange
      const state = createExecutionState();

      // Act
      const result = cmdCreateNode(state, systemActor, "node-1", "task");

      // Assert
      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("NODE_CREATED");
      expect(result.events[0].payload).toHaveProperty("nodeId", "node-1");
      expect(result.events[0].payload).toHaveProperty("nodeType", "task");
    });

    it("should be idempotent if node already exists", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
        }
      };
      const result = cmdCreateNode(state, systemActor, "node-1", "task");

      expect(result.events).toHaveLength(0);
    });

    it("should throw when execution is terminal", () => {
      const state = createExecutionState({ status: "COMPLETED" });
      
      expect(() => cmdCreateNode(state, systemActor, "node-1", "task")).toThrow(DomainError);
    });
  });

  describe("cmdStartNode", () => {
    it("should create NODE_STARTED event for ready node", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "READY", attempt: 0 }
        }
      };
      const result = cmdStartNode(state, systemActor, "node-1", 1, "worker-1");

      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("NODE_STARTED");
      expect(result.events[0].payload).toHaveProperty("nodeId", "node-1");
      expect(result.events[0].payload).toHaveProperty("attempt", 1);
      expect(result.events[0].payload).toHaveProperty("workerId", "worker-1");
    });

    it("should work for IDLE node", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "IDLE", attempt: 0 }
        }
      };
      const result = cmdStartNode(state, systemActor, "node-1", 1);

      expect(result.events).toHaveLength(1);
      expect(result.events[0].type).toBe("NODE_STARTED");
    });

    it("should throw for invalid node status", () => {
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "RUNNING", attempt: 1 }
        }
      };
      
      expect(() => cmdStartNode(state, systemActor, "node-1", 1)).toThrow(DomainError);
    });
  });

  describe("applyEvents", () => {
    it("should apply multiple events in order", () => {
      // Arrange
      const state = createExecutionState();
      const events = [
        {
          eventId: "e1",
          executionId: "exec-1",
          type: "NODE_CREATED",
          occurredAt: new Date().toISOString(),
          actor: systemActor,
          schemaVersion: 1 as const,
          payload: { nodeId: "node-1", nodeType: "task" }
        },
        {
          eventId: "e2",
          executionId: "exec-1",
          type: "NODE_READY",
          occurredAt: new Date().toISOString(),
          actor: systemActor,
          schemaVersion: 1 as const,
          payload: { nodeId: "node-1" }
        }
      ];

      // Act
      const result = applyEvents(state, events);

      // Assert
      expect(result.nodes["node-1"]).toBeDefined();
      expect(result.nodes["node-1"].status).toBe("READY");
    });

    it("should return same state for empty events", () => {
      // Arrange
      const state = createExecutionState();

      // Act
      const result = applyEvents(state, []);

      // Assert
      expect(result).toEqual(state);
    });
  });
});
