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
    it("同一入力で一貫したハッシュを生成する", () => {
      // Arrange
      const body = { key: "value" };

      // Act
      const hash1 = IdempotencyService.hashRequest(body);
      const hash2 = IdempotencyService.hashRequest(body);

      // Assert
      expect(hash1).toBe(hash2);
      expect(hash1).toHaveLength(64); // SHA256 hex string
    });

    it("異なる入力で異なるハッシュを生成する", () => {
      // Act
      const hash1 = IdempotencyService.hashRequest({ key: "value1" });
      const hash2 = IdempotencyService.hashRequest({ key: "value2" });

      // Assert
      expect(hash1).not.toBe(hash2);
    });

    it("null 入力を扱う", () => {
      // Act
      const hash = IdempotencyService.hashRequest(null);

      // Assert
      expect(hash).toBeDefined();
    });
  });

  describe("load", () => {
    it("冪等キーが無いとき null を返す", async () => {
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

    it("冪等キーが存在するとき保存結果を返す", async () => {
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
    it("冪等キーを保存する", async () => {
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
    it("冪等キーが無いとき null を返す", async () => {
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

    it("リクエストハッシュが一致するとき保存レスポンスを返す", async () => {
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

    it("リクエストハッシュが一致しないとき conflict エラーをスローする", async () => {
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
