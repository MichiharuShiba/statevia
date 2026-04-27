export const SUPPORTED_LOCALES = ["ja", "en"] as const;

export type Locale = (typeof SUPPORTED_LOCALES)[number];

export const DEFAULT_LOCALE: Locale = "ja";

/**
 * Cookie や外部入力のロケール値を安全に正規化する。
 */
export function resolveLocale(rawLocale: string | undefined): Locale {
  return rawLocale === "en" ? "en" : "ja";
}

/**
 * Intl API に渡す日時ロケールを返す。
 */
export function getDateTimeLocale(locale: Locale): string {
  return locale === "en" ? "en-US" : "ja-JP";
}

