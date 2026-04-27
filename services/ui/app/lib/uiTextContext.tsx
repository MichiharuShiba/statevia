"use client";

import { createContext, useContext, useMemo, type ReactNode } from "react";
import { DEFAULT_LOCALE, type Locale } from "./i18n";
import { getUiText } from "./uiTextLocale";
import type { UiText } from "./uiText";

type UiTextContextValue = {
  locale: Locale;
  uiText: UiText;
};

const UiTextContext = createContext<UiTextContextValue | null>(null);

type UiTextProviderProps = Readonly<{
  locale: Locale;
  children: ReactNode;
}>;

/**
 * 画面全体へ現在ロケールの UI 文言を提供する。
 */
export function UiTextProvider({ locale, children }: UiTextProviderProps) {
  const value = useMemo<UiTextContextValue>(() => ({ locale, uiText: getUiText(locale) }), [locale]);
  return <UiTextContext.Provider value={value}>{children}</UiTextContext.Provider>;
}

/**
 * 現在ロケールの UI 文言辞書を取得する。
 */
export function useUiText(): UiText {
  const contextValue = useContext(UiTextContext);
  return contextValue?.uiText ?? getUiText(DEFAULT_LOCALE);
}

/**
 * 現在ロケールを取得する。
 */
export function useLocale(): Locale {
  const contextValue = useContext(UiTextContext);
  return contextValue?.locale ?? DEFAULT_LOCALE;
}

/**
 * UI 文言とロケールをまとめて取得する。
 */
export function useI18n(): { locale: Locale; uiText: UiText } {
  const contextValue = useContext(UiTextContext);
  if (contextValue) return contextValue;
  return { locale: DEFAULT_LOCALE, uiText: getUiText(DEFAULT_LOCALE) };
}

