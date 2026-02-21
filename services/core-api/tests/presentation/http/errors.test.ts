/**
 * HTTP Errors のユニットテスト
 * 各テストは Arrange（準備）- Act（実行）- Assert（検証）のパターンで構成する。
 */
import { describe, it, expect } from "vitest";
import { HttpError, notFound, conflict, unprocessable } from "../../../src/presentation/http/errors.js";

describe("HTTP Errors", () => {
  describe("HttpError", () => {
    it("should create error with status and message", () => {
      // Arrange & Act
      const error = new HttpError(404, "Not found");

      // Assert
      expect(error).toBeInstanceOf(Error);
      expect(error.status).toBe(404);
      expect(error.message).toBe("Not found");
      expect(error.details).toBeUndefined();
    });

    it("should create error with details", () => {
      // Arrange
      const details = { resourceId: "123" };

      // Act
      const error = new HttpError(404, "Not found", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });

  describe("notFound", () => {
    it("should create 404 HttpError", () => {
      // Act
      const error = notFound("Resource not found");

      // Assert
      expect(error).toBeInstanceOf(HttpError);
      expect(error.status).toBe(404);
      expect(error.message).toBe("Resource not found");
    });

    it("should include details if provided", () => {
      // Arrange
      const details = { resourceId: "123" };

      // Act
      const error = notFound("Resource not found", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });

  describe("conflict", () => {
    it("should create 409 HttpError", () => {
      // Act
      const error = conflict("Resource conflict");

      // Assert
      expect(error).toBeInstanceOf(HttpError);
      expect(error.status).toBe(409);
      expect(error.message).toBe("Resource conflict");
    });

    it("should include details if provided", () => {
      // Arrange
      const details = { resourceId: "123" };

      // Act
      const error = conflict("Resource conflict", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });

  describe("unprocessable", () => {
    it("should create 422 HttpError", () => {
      // Act
      const error = unprocessable("Validation failed");

      // Assert
      expect(error).toBeInstanceOf(HttpError);
      expect(error.status).toBe(422);
      expect(error.message).toBe("Validation failed");
    });

    it("should include details if provided", () => {
      // Arrange
      const details = { field: "email", reason: "invalid format" };

      // Act
      const error = unprocessable("Validation failed", details);

      // Assert
      expect(error.details).toEqual(details);
    });
  });
});
