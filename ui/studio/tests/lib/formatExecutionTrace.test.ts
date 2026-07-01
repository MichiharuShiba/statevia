import { describe, expect, it } from "vitest";
import { formatTracePayload } from "../../app/lib/formatExecutionTrace";

describe("formatTracePayload", () => {
  it("undefined は空文字、null は null 文字列", () => {
    expect(formatTracePayload(undefined)).toBe("");
    expect(formatTracePayload(null)).toBe("null");
  });

  it("オブジェクトは JSON 整形、プリミティブは文字列化する", () => {
    expect(formatTracePayload({ a: 1 })).toBe("{\n  \"a\": 1\n}");
    expect(formatTracePayload("raw")).toBe("raw");
    expect(formatTracePayload(42)).toBe("42");
    expect(formatTracePayload(true)).toBe("true");
    expect(formatTracePayload(10n)).toBe("10");
  });

  it("symbol と function を表示用に変換する", () => {
    expect(formatTracePayload(Symbol("s"))).toBe("s");
    expect(formatTracePayload(function namedFn() {})).toBe("namedFn");
    expect(formatTracePayload(() => {})).toBe("function");
  });
});
