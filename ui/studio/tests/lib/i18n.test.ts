import { describe, expect, it } from "vitest";
import { DEFAULT_LOCALE, getDateTimeLocale, resolveLocale } from "../../app/lib/i18n";

describe("i18n", () => {
  it("resolveLocale は en 以外を ja に正規化する", () => {
    expect(resolveLocale("en")).toBe("en");
    expect(resolveLocale(undefined)).toBe(DEFAULT_LOCALE);
    expect(resolveLocale("fr")).toBe("ja");
  });

  it("getDateTimeLocale はロケールに応じた Intl ロケールを返す", () => {
    expect(getDateTimeLocale("ja")).toBe("ja-JP");
    expect(getDateTimeLocale("en")).toBe("en-US");
  });
});
