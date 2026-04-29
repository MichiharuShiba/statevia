export const SUPPORTED_THEMES = ["light", "dark"] as const;

export type Theme = (typeof SUPPORTED_THEMES)[number];

export const DEFAULT_THEME: Theme = "light";

/**
 * 文字列がサポート対象テーマかを判定する。
 */
export function isTheme(value: string): value is Theme {
  return value === "light" || value === "dark";
}

/**
 * Cookie 等の外部入力をテーマへ正規化する。
 * サポート外の値は null を返し、呼び出し側でフォールバック判定する。
 */
export function resolveTheme(rawTheme: string | undefined): Theme | null {
  if (!rawTheme) return null;
  return isTheme(rawTheme) ? rawTheme : null;
}
