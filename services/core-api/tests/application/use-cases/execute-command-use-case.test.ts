/**
 * ExecuteCommandUseCase のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { executeCommandUseCase, ExecuteCommandRequest } from "../../../src/application/use-cases/execute-command-use-case.js";
import { ExecutionRepository } from "../../../src/infrastructure/persistence/repositories/execution-repository.js";
import { EventStore } from "../../../src/infrastructure/persistence/repositories/event-store.js";
import { pool } from "../../../src/infrastructure/persistence/db.js";
import { ExecutionState } from "../../../src/domain/value-objects/execution-state.js";
import { EventEnvelope } from "../../../src/domain/value-objects/event-envelope.js";
import { Actor } from "../../../src/domain/value-objects/actor.js";

// モック
vi.mock("../../../src/infrastructure/persistence/db.js", () => ({
  pool: {
    connect: vi.fn()
  }
}));

vi.mock("../../../src/infrastructure/persistence/repositories/execution-repository.js");
vi.mock("../../../src/infrastructure/persistence/repositories/event-store.js");

describe("ExecuteCommandUseCase", () => {
  let mockClient: any;
  let initialState: ExecutionState;

  beforeEach(() => {
    vi.clearAllMocks();
    
    mockClient = {
      query: vi.fn().mockResolvedValue({ rowCount: 1 }),
      release: vi.fn()
    };
    
    (pool.connect as any).mockResolvedValue(mockClient);
    
    initialState = {
      executionId: "exec-123",
      graphId: "graph-1",
      status: "ACTIVE",
      version: 1,
      nodes: {
        "node-1": {
          nodeId: "node-1",
          nodeType: "task",
          status: "READY",
          attempt: 0
        }
      }
    };
    
    (ExecutionRepository.loadWithLock as any) = vi.fn().mockResolvedValue(initialState);
    (EventStore.appendEvents as any) = vi.fn().mockResolvedValue(undefined);
    (ExecutionRepository.save as any) = vi.fn().mockResolvedValue(undefined);
  });

  it("should execute command and return new state", async () => {
    // Arrange
    const mockEvent: EventEnvelope = {
      eventId: "event-1",
      executionId: "exec-123",
      type: "NODE_STARTED",
      occurredAt: new Date().toISOString(),
      actor: { kind: "system" },
      schemaVersion: 1,
      payload: { nodeId: "node-1", attempt: 1 }
    };
    const request: ExecuteCommandRequest = {
      executionId: "exec-123",
      commandFn: (state) => ({
        events: [mockEvent]
      })
    };

    // Act
    const result = await executeCommandUseCase(request);

    // Assert
    expect(ExecutionRepository.loadWithLock).toHaveBeenCalledWith(mockClient, "exec-123");
    expect(EventStore.appendEvents).toHaveBeenCalledWith(mockClient, [mockEvent]);
    expect(ExecutionRepository.save).toHaveBeenCalled();
    
    expect(mockClient.query).toHaveBeenCalledWith("begin");
    expect(mockClient.query).toHaveBeenCalledWith("commit");
    expect(mockClient.release).toHaveBeenCalled();
    
    expect(result).toBeDefined();
    expect(result.executionId).toBe("exec-123");
  });

  it("should handle empty events array", async () => {
    // Arrange
    const request: ExecuteCommandRequest = {
      executionId: "exec-123",
      commandFn: () => ({ events: [] })
    };

    // Act
    const result = await executeCommandUseCase(request);

    // Assert
    expect(EventStore.appendEvents).toHaveBeenCalledWith(mockClient, []);
    expect(result).toBeDefined();
  });

  it("should rollback on error", async () => {
    // Arrange
    const request: ExecuteCommandRequest = {
      executionId: "exec-123",
      commandFn: () => {
        throw new Error("Command error");
      }
    };

    // Act & Assert
    await expect(executeCommandUseCase(request)).rejects.toThrow("Command error");
    
    expect(mockClient.query).toHaveBeenCalledWith("rollback");
    expect(mockClient.release).toHaveBeenCalled();
  });

  it("should rollback when repository throws", async () => {
    // Arrange
    (ExecutionRepository.loadWithLock as any).mockRejectedValue(new Error("DB error"));
    const request: ExecuteCommandRequest = {
      executionId: "exec-123",
      commandFn: () => ({ events: [] })
    };

    // Act & Assert
    await expect(executeCommandUseCase(request)).rejects.toThrow("DB error");
    
    expect(mockClient.query).toHaveBeenCalledWith("rollback");
    expect(mockClient.release).toHaveBeenCalled();
  });

  it("should always release client connection", async () => {
    // Arrange
    const request: ExecuteCommandRequest = {
      executionId: "exec-123",
      commandFn: () => ({ events: [] })
    };

    // Act
    await executeCommandUseCase(request);

    // Assert
    expect(mockClient.release).toHaveBeenCalled();
  });

  it("should pass correct state to command function", async () => {
    // Arrange
    let receivedState: ExecutionState | null = null;
    const request: ExecuteCommandRequest = {
      executionId: "exec-123",
      commandFn: (state) => {
        receivedState = state;
        return { events: [] };
      }
    };

    // Act
    await executeCommandUseCase(request);

    // Assert
    expect(receivedState).toEqual(initialState);
  });
});
