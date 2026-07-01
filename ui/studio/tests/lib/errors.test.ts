import { describe, expect, it } from "vitest";
import { toToastError } from "../../app/lib/errors";

describe("toToastError", () => {
  it("status 409 のとき 409 用メッセージを返す", () => {
    // Arrange
    const error = {
      status: 409,
      error: { code: "CONFLICT", message: "State conflict" }
    };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("409");
    expect(result.message).toContain("CONFLICT");
    expect(result.message).toContain("State conflict");
  });

  it("status 422 のとき 422 用メッセージを返す", () => {
    // Arrange
    const error = {
      status: 422,
      error: { code: "INVALID_INPUT", message: "Bad request" }
    };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("422");
    expect(result.message).toContain("INVALID_INPUT");
  });

  it("status 401 のとき認証用メッセージを返す", () => {
    // Arrange
    const error = {
      status: 401,
      error: { code: "UNAUTHORIZED", message: "Token required" }
    };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("401");
    expect(result.message).toContain("認証が必要です");
    expect(result.message).toContain("Token required");
  });

  it("status 403 のとき権限・テナント用メッセージを返す", () => {
    // Arrange
    const error = {
      status: 403,
      error: { code: "FORBIDDEN", message: "Tenant not specified" }
    };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("403");
    expect(result.message).toContain("権限不足またはテナント未指定");
    expect(result.message).toContain("Tenant not specified");
  });

  it("status 500 のとき 500 用メッセージを返す", () => {
    // Arrange
    const error = {
      status: 500,
      error: { code: "INTERNAL", message: "Server error" }
    };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("500");
  });

  it("ApiError でない入力のとき汎用エラーメッセージを返す", () => {
    // Arrange
    const error = new Error("something");

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("UNKNOWN");
    expect(result.message).toContain("Unknown error");
  });

  it("error に code が無いとき UNKNOWN を使う", () => {
    // Arrange
    const error = { status: 400, error: { message: "x" } } as never;

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).toContain("UNKNOWN");
  });

  it("error.message が undefined のとき Unknown error を使う", () => {
    // Arrange
    const error = { status: 400, error: { code: "X" } } as never;

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).toContain("Unknown error");
  });

  it("その他の status（例: 404）のとき汎用メッセージを返す", () => {
    // Arrange
    const error = {
      status: 404,
      error: { code: "NOT_FOUND", message: "Execution not found" }
    };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("NOT_FOUND");
    expect(result.message).toContain("Execution not found");
  });

  it("message が空の error を扱う", () => {
    // Arrange
    const error = { status: 500, error: { code: "ERR", message: "" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("ERR");
  });

  it("null 入力を扱う", () => {
    // Arrange
    const error = null;

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("Unknown error");
  });

  it("undefined 入力を扱う", () => {
    // Arrange
    const error = undefined;

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("UNKNOWN");
  });
});

describe("toToastError (境界値)", () => {
  it("status 408 は汎用メッセージ（409 の境界外）", () => {
    // Arrange
    const error = { status: 408, error: { code: "TIMEOUT", message: "Request timeout" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).not.toContain("409");
    expect(result.message).toContain("TIMEOUT");
  });

  it("status 410 は汎用メッセージ（409 の境界外）", () => {
    // Arrange
    const error = { status: 410, error: { code: "GONE", message: "Gone" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).not.toContain("409");
    expect(result.message).toContain("GONE");
  });

  it("status 421 は汎用メッセージ（422 の境界外）", () => {
    // Arrange
    const error = { status: 421, error: { code: "X", message: "y" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).not.toContain("422");
  });

  it("status 423 は汎用メッセージ（422 の境界外）", () => {
    // Arrange
    const error = { status: 423, error: { code: "LOCKED", message: "Locked" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).not.toContain("422");
  });

  it("status 499 は汎用メッセージ（500 の境界外）", () => {
    // Arrange
    const error = { status: 499, error: { code: "X", message: "y" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).not.toContain("500");
  });

  it("status 501 は汎用メッセージ（500 の境界外）", () => {
    // Arrange
    const error = { status: 501, error: { code: "NOT_IMPLEMENTED", message: "Not implemented" } };

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.message).not.toContain("500");
    expect(result.message).toContain("NOT_IMPLEMENTED");
  });

  it("error オブジェクトで error が null の場合は UNKNOWN / Unknown error", () => {
    // Arrange
    const error = { status: 500, error: null } as never;

    // Act
    const result = toToastError(error);

    // Assert
    expect(result.tone).toBe("error");
    expect(result.message).toContain("UNKNOWN");
    expect(result.message).toContain("Unknown error");
  });
});
