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
    it("ACTIVE のときスローしない", () => {
      // Arrange
      const state = createExecutionState({ status: "ACTIVE" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).not.toThrow();
    });

    it("COMPLETED のとき DomainError をスローする", () => {
      // Arrange
      const state = createExecutionState({ status: "COMPLETED" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).toThrow(DomainError);
      expect(() => ensureNotTerminalExecution(state)).toThrow("Execution is terminal");
    });

    it("FAILED のとき DomainError をスローする", () => {
      // Arrange
      const state = createExecutionState({ status: "FAILED" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).toThrow(DomainError);
      expect(() => ensureNotTerminalExecution(state)).toThrow("Execution is terminal");
    });

    it("CANCELED のとき DomainError をスローする", () => {
      // Arrange
      const state = createExecutionState({ status: "CANCELED" });

      // Act & Assert
      expect(() => ensureNotTerminalExecution(state)).toThrow(DomainError);
      expect(() => ensureNotTerminalExecution(state)).toThrow("Execution is terminal");
    });
  });

  describe("ensureCancelNotRequested", () => {
    it("cancelRequestedAt が無いときスローしない", () => {
      // Arrange
      const state = createExecutionState();

      // Act & Assert
      expect(() => ensureCancelNotRequested(state)).not.toThrow();
    });

    it("cancelRequestedAt が設定されているとき DomainError をスローする", () => {
      // Arrange
      const state = createExecutionState({ cancelRequestedAt: "2024-01-01T00:00:00Z" });

      // Act & Assert
      expect(() => ensureCancelNotRequested(state)).toThrow(DomainError);
      expect(() => ensureCancelNotRequested(state)).toThrow("Execution is cancel-requested");
    });
  });

  describe("getNodeOrThrow", () => {
    it("ノードが存在するときそのノードを返す", () => {
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

    it("ノードが存在しないとき DomainError をスローする", () => {
      // Arrange
      const state = createExecutionState();

      // Act & Assert
      expect(() => getNodeOrThrow(state, "node-999")).toThrow(DomainError);
      expect(() => getNodeOrThrow(state, "node-999")).toThrow("Node not found");
    });
  });

  describe("ensureNodeStatus", () => {
    it("許可リストに status があるときスローしない", () => {
      // Act & Assert
      expect(() => ensureNodeStatus("RUNNING", ["RUNNING", "WAITING"])).not.toThrow();
    });

    it("許可リストに status が無いとき DomainError をスローする", () => {
      // Act & Assert
      expect(() => ensureNodeStatus("IDLE", ["RUNNING", "WAITING"])).toThrow(DomainError);
      expect(() => ensureNodeStatus("IDLE", ["RUNNING", "WAITING"])).toThrow("Node status invalid");
    });

    it("複数許可 status で動作する", () => {
      // Act & Assert
      expect(() => ensureNodeStatus("READY", ["READY", "IDLE"])).not.toThrow();
      expect(() => ensureNodeStatus("IDLE", ["READY", "IDLE"])).not.toThrow();
    });
  });
});
