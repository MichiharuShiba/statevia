import { describe, expect, it } from "vitest";
import { DEFAULT_THEME, isTheme, resolveTheme } from "../../app/lib/theme";

describe("theme", () => {
  it("isTheme は light / dark のみ true", () => {
    expect(isTheme("light")).toBe(true);
    expect(isTheme("dark")).toBe(true);
    expect(isTheme("system")).toBe(false);
  });

  it("resolveTheme は未指定・不正値で null、有効値で Theme を返す", () => {
    expect(resolveTheme(undefined)).toBeNull();
    expect(resolveTheme("invalid")).toBeNull();
    expect(resolveTheme("dark")).toBe("dark");
    expect(DEFAULT_THEME).toBe("light");
  });
});
