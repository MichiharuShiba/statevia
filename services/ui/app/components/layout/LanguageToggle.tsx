"use client";

import { useRouter } from "next/navigation";
import type { Locale } from "../../lib/i18n";

type LanguageToggleProps = Readonly<{
  locale: Locale;
}>;

/**
 * 共通ヘッダーで表示言語を切り替える。
 */
export function LanguageToggle({ locale }: LanguageToggleProps) {
  const router = useRouter();

  const handleChangeLocale = (nextLocale: Locale) => {
    if (nextLocale === locale) return;
    document.cookie = `ui-lang=${nextLocale}; Path=/; Max-Age=31536000; SameSite=Lax`;
    router.refresh();
  };

  return (
    <div className="inline-flex items-center gap-1 rounded-md border border-white/20 p-1 text-xs">
      <button
        type="button"
        className={`rounded px-2 py-1 ${locale === "ja" ? "bg-white/20 text-white" : "text-[var(--brand-header-fg-muted)] hover:text-[var(--brand-header-fg)]"}`}
        onClick={() => handleChangeLocale("ja")}
        aria-pressed={locale === "ja"}
      >
        JP
      </button>
      <button
        type="button"
        className={`rounded px-2 py-1 ${locale === "en" ? "bg-white/20 text-white" : "text-[var(--brand-header-fg-muted)] hover:text-[var(--brand-header-fg)]"}`}
        onClick={() => handleChangeLocale("en")}
        aria-pressed={locale === "en"}
      >
        EN
      </button>
    </div>
  );
}

