/**
 * HTTP Errors のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect } from "vitest";
import { HttpError, notFound, conflict, unprocessable } from "../../../src/presentation/http/errors.js";

describe("HTTP Errors", () => {
  describe("HttpError", () => {
    it("status と message でエラーを生成する", () => {
      // Arrange & Act
      const error = new HttpError(404, "Not found");

      // Assert
      expect(error).toBeInstanceOf(Error);
      expect(error.status).toBe(404);
      expect(error.message).toBe("Not found");
      expect(error.details).toBeUndefined();
    });

    it("details 付きでエラーを生成する", () => {
      // Arrange
      const details = { resourceId: "123" };

      // Act
      const error = new HttpError(404, "Not found", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });

  describe("notFound", () => {
    it("404 の HttpError を生成する", () => {
      // Act
      const error = notFound("Resource not found");

      // Assert
      expect(error).toBeInstanceOf(HttpError);
      expect(error.status).toBe(404);
      expect(error.message).toBe("Resource not found");
    });

    it("details が渡されたとき含める", () => {
      // Arrange
      const details = { resourceId: "123" };

      // Act
      const error = notFound("Resource not found", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });

  describe("conflict", () => {
    it("409 の HttpError を生成する", () => {
      // Act
      const error = conflict("Resource conflict");

      // Assert
      expect(error).toBeInstanceOf(HttpError);
      expect(error.status).toBe(409);
      expect(error.message).toBe("Resource conflict");
    });

    it("details が渡されたとき含める", () => {
      // Arrange
      const details = { resourceId: "123" };

      // Act
      const error = conflict("Resource conflict", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });

  describe("unprocessable", () => {
    it("422 の HttpError を生成する", () => {
      // Act
      const error = unprocessable("Validation failed");

      // Assert
      expect(error).toBeInstanceOf(HttpError);
      expect(error.status).toBe(422);
      expect(error.message).toBe("Validation failed");
    });

    it("details が渡されたとき含める", () => {
      // Arrange
      const details = { field: "email", reason: "invalid format" };

      // Act
      const error = unprocessable("Validation failed", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });
});
