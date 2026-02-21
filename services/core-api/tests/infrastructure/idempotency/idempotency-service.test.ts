/**
 * IdempotencyService のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { IdempotencyService } from "../../../src/infrastructure/idempotency/idempotency-service.js";
import { pool } from "../../../src/infrastructure/persistence/db.js";
import { conflict } from "../../../src/presentation/http/errors.js";

// モック
vi.mock("../../../src/infrastructure/persistence/db.js", () => ({
  pool: {
    query: vi.fn()
  }
}));

describe("IdempotencyService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("hashRequest", () => {
    it("should generate consistent hash for same input", () => {
      // Arrange
      const body = { key: "value" };

      // Act
      const hash1 = IdempotencyService.hashRequest(body);
      const hash2 = IdempotencyService.hashRequest(body);

      // Assert
      expect(hash1).toBe(hash2);
      expect(hash1).toHaveLength(64); // SHA256 hex string
    });

    it("should generate different hash for different input", () => {
      // Act
      const hash1 = IdempotencyService.hashRequest({ key: "value1" });
      const hash2 = IdempotencyService.hashRequest({ key: "value2" });

      // Assert
      expect(hash1).not.toBe(hash2);
    });

    it("should handle null input", () => {
      // Act
      const hash = IdempotencyService.hashRequest(null);

      // Assert
      expect(hash).toBeDefined();
    });
  });

  describe("load", () => {
    it("should return null if idempotency key is not found", async () => {
      // Arrange
      (pool.query as any).mockResolvedValueOnce({ rowCount: 0 });

      // Act
      const result = await IdempotencyService.load("key-123", "/executions");

      // Assert
      expect(result).toBeNull();
      expect(pool.query).toHaveBeenCalledWith(
        expect.stringContaining("select"),
        ["key-123", "/executions"]
      );
    });

    it("should return stored result if idempotency key exists", async () => {
      // Arrange
      const storedData = {
        request_hash: "hash-123",
        response_status: 202,
        response_body: { executionId: "exec-1" }
      };
      (pool.query as any).mockResolvedValueOnce({
        rowCount: 1,
        rows: [storedData]
      });

      // Act
      const result = await IdempotencyService.load("key-123", "/executions");

      // Assert
      expect(result).toEqual({
        requestHash: "hash-123",
        responseStatus: 202,
        responseBody: { executionId: "exec-1" }
      });
    });
  });

  describe("save", () => {
    it("should save idempotency key", async () => {
      // Arrange
      (pool.query as any).mockResolvedValueOnce({ rowCount: 1 });

      // Act
      await IdempotencyService.save("key-123", "/executions", "hash-123", 202, { executionId: "exec-1" });

      // Assert
      expect(pool.query).toHaveBeenCalledWith(
        expect.stringContaining("insert"),
        ["key-123", "/executions", "hash-123", 202, { executionId: "exec-1" }]
      );
    });
  });

  describe("enforceOrReplay", () => {
    it("should return null if idempotency key is not found", async () => {
      // Arrange
      (pool.query as any).mockResolvedValueOnce({ rowCount: 0 });

      // Act
      const result = await IdempotencyService.enforceOrReplay({
        idempotencyKey: "key-123",
        endpoint: "/executions",
        requestHash: "hash-123"
      });

      // Assert
      expect(result).toBeNull();
    });

    it("should replay response if request hash matches", async () => {
      // Arrange
      (pool.query as any).mockResolvedValueOnce({
        rowCount: 1,
        rows: [{
          request_hash: "hash-123",
          response_status: 202,
          response_body: { executionId: "exec-1" }
        }]
      });

      // Act
      const result = await IdempotencyService.enforceOrReplay({
        idempotencyKey: "key-123",
        endpoint: "/executions",
        requestHash: "hash-123"
      });

      // Assert
      expect(result).toEqual({
        status: 202,
        body: { executionId: "exec-1" }
      });
    });

    it("should throw conflict error if request hash does not match", async () => {
      // Arrange
      (pool.query as any).mockResolvedValueOnce({
        rowCount: 1,
        rows: [{
          request_hash: "hash-456",
          response_status: 202,
          response_body: { executionId: "exec-1" }
        }]
      });

      // Act & Assert
      await expect(
        IdempotencyService.enforceOrReplay({
          idempotencyKey: "key-123",
          endpoint: "/executions",
          requestHash: "hash-123"
        })
      ).rejects.toThrow();
    });
  });
});
