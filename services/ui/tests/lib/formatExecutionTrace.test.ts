import { describe, expect, it } from "vitest";
import { formatTracePayload } from "../../app/lib/formatExecutionTrace";

describe("formatExecutionTrace", () => {
  it("formatTracePayload はオブジェクトを JSON 整形する", () => {
    expect(formatTracePayload({ a: 1 })).toBe("{\n  \"a\": 1\n}");
    expect(formatTracePayload(null)).toBe("null");
  });
});
