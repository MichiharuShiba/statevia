"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { Locale } from "../../lib/i18n";
import type { Theme } from "../../lib/theme";
import { useUiText } from "../../lib/uiTextContext";
import { AdminNavLinks } from "./AdminNavLinks";
import { LanguageToggle } from "./LanguageToggle";
import { ThemeToggle } from "./ThemeToggle";

type AppHeaderProps = Readonly<{
  theme: Theme;
  locale: Locale;
}>;

/**
 * 共通ヘッダー。ログイン画面ではアプリ内ナビを表示しない。
 */
export function AppHeader({ theme, locale }: AppHeaderProps) {
  const pathname = usePathname();
  const uiText = useUiText();
  const isLoginPage = pathname === "/login";
  const brandHref = isLoginPage ? "/login" : "/dashboard";

  return (
    <header className="border-b border-[var(--md-sys-color-outline)] bg-[var(--brand-header-bg)] text-[var(--brand-header-fg)]">
      <div className="mx-auto flex max-w-[min(1400px,calc(100%-2rem))] flex-wrap items-center justify-between gap-3 px-4 py-3">
        <Link href={brandHref} className="inline-flex items-center gap-2">
          <Image
            src="/brand/icon-mark.png"
            alt="statevia"
            width={32}
            height={32}
            className="h-8 w-8 rounded-md border border-white/20 object-cover"
            priority
          />
          <span className="text-[1.75rem] font-semibold leading-none tracking-wide">
            <span>state</span>
            <span className="text-emerald-400">via</span>
          </span>
        </Link>
        <div className="flex items-center gap-3">
          {isLoginPage ? null : (
            <nav className="flex flex-wrap items-center gap-3 text-sm text-[var(--brand-header-fg-muted)]">
              <Link href="/dashboard" className="hover:text-[var(--brand-header-fg)] hover:underline">
                {uiText.navigation.dashboard}
              </Link>
              <Link href="/definitions" className="hover:text-[var(--brand-header-fg)] hover:underline">
                {uiText.navigation.definitions}
              </Link>
              <Link href="/executions" className="hover:text-[var(--brand-header-fg)] hover:underline">
                {uiText.navigation.executions}
              </Link>
              <AdminNavLinks />
            </nav>
          )}
          <ThemeToggle theme={theme} />
          <LanguageToggle locale={locale} />
        </div>
      </div>
    </header>
  );
}
