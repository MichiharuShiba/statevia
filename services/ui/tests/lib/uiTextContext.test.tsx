import { describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { UiTextProvider, useI18n, useLocale, useUiText } from "../../app/lib/uiTextContext";
import { getUiText } from "../../app/lib/uiTextLocale";

function wrapper(locale: "ja" | "en") {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <UiTextProvider locale={locale}>{children}</UiTextProvider>;
  };
}

describe("uiTextContext", () => {
  it("Provider 外では既定ロケールの文言を返す", () => {
    const { result: textResult } = renderHook(() => useUiText());
    const { result: localeResult } = renderHook(() => useLocale());
    const { result: i18nResult } = renderHook(() => useI18n());

    expect(textResult.current).toEqual(getUiText("ja"));
    expect(localeResult.current).toBe("ja");
    expect(i18nResult.current.locale).toBe("ja");
  });

  it("Provider 内では指定ロケールの文言を返す", () => {
    const { result } = renderHook(
      () => ({
        uiText: useUiText(),
        locale: useLocale(),
        i18n: useI18n()
      }),
      { wrapper: wrapper("en") }
    );

    expect(result.current.locale).toBe("en");
    expect(result.current.uiText).toEqual(getUiText("en"));
    expect(result.current.i18n.uiText).toEqual(getUiText("en"));
  });
});
