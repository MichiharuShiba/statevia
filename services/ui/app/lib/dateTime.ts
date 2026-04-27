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

