import { describe, expect, it } from "vitest";
import { formatExecutionDuration, formatExecutionInstant } from "../../app/lib/dateTime";

describe("dateTime", () => {
  it("formatExecutionInstant はロケールに応じて日時を返す", () => {
    const s = formatExecutionInstant("2026-01-15T10:00:00.000Z", "ja");
    expect(s.length).toBeGreaterThan(0);
    expect(s).not.toBe("2026-01-15T10:00:00.000Z");
  });

  it("formatExecutionInstant はミリ秒まで含める", () => {
    const ja = formatExecutionInstant("2026-01-15T10:00:00.789Z", "ja");
    expect(ja).toMatch(/789/);
    const en = formatExecutionInstant("2026-01-15T10:00:00.789Z", "en");
    expect(en).toMatch(/789/);
  });

  it("formatExecutionDuration は ms / s / min 表記に切り替える", () => {
    expect(
      formatExecutionDuration("2026-01-15T10:00:00.000Z", "2026-01-15T10:00:00.100Z")
    ).toBe("100 ms");
    expect(
      formatExecutionDuration("2026-01-15T10:00:00.000Z", "2026-01-15T10:00:01.000Z")
    ).toBe("1.0 s");
    expect(
      formatExecutionDuration("2026-01-15T10:00:00.000Z", "2026-01-15T10:02:30.000Z")
    ).toMatch(/2 min/);
  });

  it("formatExecutionDuration は欠損や不正な区間で null", () => {
    expect(formatExecutionDuration(undefined, "2026-01-15T10:00:01.000Z")).toBeNull();
    expect(formatExecutionDuration("2026-01-15T10:00:00.000Z", null)).toBeNull();
    expect(formatExecutionDuration("2026-01-15T10:00:05.000Z", "2026-01-15T10:00:01.000Z")).toBeNull();
  });
});
