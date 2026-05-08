import type { Locale } from "./i18n";
import { getDateTimeLocale } from "./i18n";

/**
 * ロケールに応じて日時文字列を整形する共通ユーティリティ。
 */
export function formatDateTimeLocalized(
  iso: string | null | undefined,
  locale: string,
  options?: Intl.DateTimeFormatOptions,
  fallback = "—"
): string {
  if (!iso) return fallback;
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return iso;
  const effectiveOptions: Intl.DateTimeFormatOptions = options ?? { dateStyle: "short", timeStyle: "short" };
  return parsed.toLocaleString(locale, effectiveOptions);
}

/**
 * 実行グラフ由来の ISO 時刻を画面表示用に整形する。
 */
export function formatExecutionInstant(iso: string, locale: Locale): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return iso;
  return new Date(t).toLocaleString(getDateTimeLocale(locale));
}

/**
 * 開始・終了 ISO 時刻から経過時間の表示文字列を返す。計算できないときは null。
 */
export function formatExecutionDuration(startIso: string | undefined, endIso: string | null | undefined): string | null {
  if (!startIso || endIso == null || endIso === "") return null;
  const a = Date.parse(startIso);
  const b = Date.parse(endIso);
  if (Number.isNaN(a) || Number.isNaN(b) || b < a) return null;
  const ms = b - a;
  if (ms < 1000) return `${Math.round(ms)} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`;
  const m = Math.floor(ms / 60_000);
  const s = ((ms % 60_000) / 1000).toFixed(0);
  return `${m} min ${s} s`;
}

