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
    expect(isJsonPathExpression("$['input'].x")).toBe(true);
    expect(isJsonPathExpression("$")).toBe(true);
    expect(coerceScalarForSchema("$.config.timeout", schema)).toBe("$.config.timeout");
    expect(coerceScalarForSchema("$.states['a.b'].output.x", schema)).toBe(
      "$.states['a.b'].output.x"
    );
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

  it("number / boolean / path / oneOf / 空文字を正規化する", () => {
    expect(coerceScalarForSchema("", { type: "string" })).toBeUndefined();
    expect(coerceScalarForSchema("1.5", { type: "number" })).toBe(1.5);
    expect(coerceScalarForSchema("true", { type: "boolean" })).toBe(true);
    expect(coerceScalarForSchema("false", { type: "boolean" })).toBe(false);
    expect(coerceScalarForSchema("$.x", { type: "string", "x-statevia-valueKind": "path" })).toBe(
      "$.x"
    );
    expect(
      coerceScalarForSchema("42", {
        oneOf: [{ type: "integer" }, { type: "string" }]
      })
    ).toBe(42);
    expect(
      coerceScalarForSchema("not-a-number", {
        oneOf: [{ type: "integer" }]
      })
    ).toBe("not-a-number");
    expect(coerceScalarForSchema("hello", { type: "string" })).toBe("hello");
    expect(coerceScalarForSchema("abc", { type: "integer" })).toBe("abc");
  });
});
