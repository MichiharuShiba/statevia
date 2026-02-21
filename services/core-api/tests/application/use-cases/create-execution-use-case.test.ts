/**
 * CreateExecutionUseCase のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { createExecutionUseCase, CreateExecutionRequest } from "../../../src/application/use-cases/create-execution-use-case.js";
import { EventStore } from "../../../src/infrastructure/persistence/repositories/event-store.js";
import { ExecutionRepository } from "../../../src/infrastructure/persistence/repositories/execution-repository.js";
import { pool } from "../../../src/infrastructure/persistence/db.js";
import { Actor } from "../../../src/domain/value-objects/actor.js";

// モック
vi.mock("../../../src/infrastructure/persistence/db.js", () => ({
  pool: {
    connect: vi.fn()
  }
}));

vi.mock("../../../src/infrastructure/persistence/repositories/event-store.js");
vi.mock("../../../src/infrastructure/persistence/repositories/execution-repository.js");

describe("CreateExecutionUseCase", () => {
  let mockClient: any;

  beforeEach(() => {
    vi.clearAllMocks();
    
    mockClient = {
      query: vi.fn().mockResolvedValue({ rowCount: 0 }),
      release: vi.fn()
    };
    
    (pool.connect as any).mockResolvedValue(mockClient);
    (EventStore.appendEvents as any) = vi.fn().mockResolvedValue(undefined);
    (ExecutionRepository.save as any) = vi.fn().mockResolvedValue(undefined);
  });

  it("should create execution with provided executionId", async () => {
    // Arrange
    const request: CreateExecutionRequest = {
      executionId: "exec-123",
      graphId: "graph-1",
      actor: { kind: "user", id: "user-1" },
      correlationId: "corr-1"
    };

    // Act
    const response = await createExecutionUseCase(request);

    // Assert
    expect(response.status).toBe(202);
    expect(response.body.executionId).toBe("exec-123");
    expect(response.body.command).toBe("CreateExecution");
    expect(response.body.accepted).toBe(true);
    expect(response.body.correlationId).toBe("corr-1");
    
    // トランザクションが開始・コミットされることを確認
    expect(mockClient.query).toHaveBeenCalledWith("begin");
    expect(mockClient.query).toHaveBeenCalledWith("commit");
    
    // executions テーブルへの挿入を確認
    expect(mockClient.query).toHaveBeenCalledWith(
      expect.stringContaining("insert into executions"),
      ["exec-123", "graph-1"]
    );
    
    // イベントの保存を確認
    expect(EventStore.appendEvents).toHaveBeenCalled();
    expect(ExecutionRepository.save).toHaveBeenCalled();
  });

  it("should generate executionId if not provided", async () => {
    // Arrange
    const request: CreateExecutionRequest = {
      graphId: "graph-1",
      actor: { kind: "system" }
    };

    // Act
    const response = await createExecutionUseCase(request);

    // Assert
    expect(response.status).toBe(202);
    expect(response.body.executionId).toBeDefined();
    expect(response.body.executionId).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
  });

  it("should handle input parameter", async () => {
    // Arrange
    const request: CreateExecutionRequest = {
      executionId: "exec-123",
      graphId: "graph-1",
      input: { key: "value" },
      actor: { kind: "user", id: "user-1" }
    };

    // Act
    const response = await createExecutionUseCase(request);

    // Assert
    expect(response.status).toBe(202);
    expect(EventStore.appendEvents).toHaveBeenCalled();
    
    // イベントのペイロードに input が含まれることを確認
    const appendCall = (EventStore.appendEvents as any).mock.calls[0];
    const events = appendCall[1];
    expect(events[0].payload).toHaveProperty("input", { key: "value" });
  });

  it("should rollback on error", async () => {
    // Arrange
    const request: CreateExecutionRequest = {
      executionId: "exec-123",
      graphId: "graph-1",
      actor: { kind: "user", id: "user-1" }
    };
    (EventStore.appendEvents as any).mockRejectedValue(new Error("DB error"));

    // Act & Assert
    await expect(createExecutionUseCase(request)).rejects.toThrow("DB error");
    
    expect(mockClient.query).toHaveBeenCalledWith("rollback");
    expect(mockClient.release).toHaveBeenCalled();
  });

  it("should always release client connection", async () => {
    // Arrange
    const request: CreateExecutionRequest = {
      executionId: "exec-123",
      graphId: "graph-1",
      actor: { kind: "user", id: "user-1" }
    };

    // Act
    await createExecutionUseCase(request);

    // Assert
    expect(mockClient.release).toHaveBeenCalled();
  });

  it("should handle missing correlationId", async () => {
    // Arrange
    const request: CreateExecutionRequest = {
      executionId: "exec-123",
      graphId: "graph-1",
      actor: { kind: "user", id: "user-1" }
    };

    // Act
    const response = await createExecutionUseCase(request);

    // Assert
    expect(response.body.correlationId).toBeNull();
  });
});
