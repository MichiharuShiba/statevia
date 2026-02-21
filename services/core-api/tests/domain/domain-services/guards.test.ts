/**
 * Guards のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect } from "vitest";
import {
  ensureNotTerminalExecution,
  ensureCancelNotRequested,
  getNodeOrThrow,
  ensureNodeStatus
} from "../../../src/domain/domain-services/guards.js";
import { ExecutionState } from "../../../src/domain/value-objects/execution-state.js";
import { DomainError } from "../../../src/domain/errors.js";

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

describe("Guards", () => {
  describe("ensureNotTerminalExecution", () => {
    it("should not throw for ACTIVE status", () => {
      // Arrange
      const state = createExecutionState({ status: "ACTIVE" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).not.toThrow();
    });

    it("should throw DomainError for COMPLETED status", () => {
      // Arrange
      const state = createExecutionState({ status: "COMPLETED" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).toThrow(DomainError);
      expect(() => ensureNotTerminalExecution(state)).toThrow("Execution is terminal");
    });

    it("should throw DomainError for FAILED status", () => {
      // Arrange
      const state = createExecutionState({ status: "FAILED" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).toThrow(DomainError);
      expect(() => ensureNotTerminalExecution(state)).toThrow("Execution is terminal");
    });

    it("should throw DomainError for CANCELED status", () => {
      // Arrange
      const state = createExecutionState({ status: "CANCELED" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).toThrow(DomainError);
      expect(() => ensureNotTerminalExecution(state)).toThrow("Execution is terminal");
    });
  });

  describe("ensureCancelNotRequested", () => {
    it("should not throw when cancelRequestedAt is undefined", () => {
      // Arrange
      const state = createExecutionState();

      // Act & Assert
      expect(() => ensureCancelNotRequested(state)).not.toThrow();
    });

    it("should throw DomainError when cancelRequestedAt is set", () => {
      // Arrange
      const state = createExecutionState({ cancelRequestedAt: "2024-01-01T00:00:00Z" });

      // Act & Assert
      expect(() => ensureCancelNotRequested(state)).toThrow(DomainError);
      expect(() => ensureCancelNotRequested(state)).toThrow("Execution is cancel-requested");
    });
  });

  describe("getNodeOrThrow", () => {
    it("should return node when it exists", () => {
      // Arrange
      const state: ExecutionState = {
        ...createExecutionState(),
        nodes: {
          "node-1": { nodeId: "node-1", nodeType: "task", status: "IDLE", attempt: 0 }
        }
      };

      // Act
      const node = getNodeOrThrow(state, "node-1");

      // Assert
      expect(node).toBeDefined();
      expect(node.nodeId).toBe("node-1");
    });

    it("should throw DomainError when node does not exist", () => {
      // Arrange
      const state = createExecutionState();

      // Act & Assert
      expect(() => getNodeOrThrow(state, "node-999")).toThrow(DomainError);
      expect(() => getNodeOrThrow(state, "node-999")).toThrow("Node not found");
    });
  });

  describe("ensureNodeStatus", () => {
    it("should not throw when status is in allowed list", () => {
      // Act & Assert
      expect(() => ensureNodeStatus("RUNNING", ["RUNNING", "WAITING"])).not.toThrow();
    });

    it("should throw DomainError when status is not in allowed list", () => {
      // Act & Assert
      expect(() => ensureNodeStatus("IDLE", ["RUNNING", "WAITING"])).toThrow(DomainError);
      expect(() => ensureNodeStatus("IDLE", ["RUNNING", "WAITING"])).toThrow("Node status invalid");
    });

    it("should work with multiple allowed statuses", () => {
      // Act & Assert
      expect(() => ensureNodeStatus("READY", ["READY", "IDLE"])).not.toThrow();
      expect(() => ensureNodeStatus("IDLE", ["READY", "IDLE"])).not.toThrow();
    });
  });
});
