import { describe, expect, it } from "vitest";
import {
  coerceScalarForSchema,
  isJsonPathExpression,
  normalizeActionInputRecord
} from "../../app/lib/actionSchema/coerceActionInputValue";

describe("coerceActionInputValue", () => {
  it("integer schema では数値文字列を integer に変換する", () => {
    const schema = { type: "integer", minimum: 1, "x-statevia-valueKind": "literalOrPath" };
    expect(coerceScalarForSchema("30", schema)).toBe(30);
  });

  it("JSONPath 式は文字列のまま保持する", () => {
    const schema = { type: "integer", "x-statevia-valueKind": "literalOrPath" };
    expect(isJsonPathExpression("$.config.timeout")).toBe(true);
    expect(coerceScalarForSchema("$.config.timeout", schema)).toBe("$.config.timeout");
  });

  it("normalizeActionInputRecord が文字列の timeout を integer に直す", () => {
    const inputSchema = {
      type: "object",
      properties: {
        timeout: { type: "integer", "x-statevia-valueKind": "literalOrPath" }
      }
    };
    const normalized = normalizeActionInputRecord({ timeout: "30", url: "https://x" }, inputSchema, [
      "timeout",
      "url"
    ]);
    expect(normalized.timeout).toBe(30);
    expect(normalized.url).toBe("https://x");
  });
});
