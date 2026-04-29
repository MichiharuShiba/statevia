import "./globals.css";
import type { Metadata } from "next";
import { cookies } from "next/headers";
import Image from "next/image";
import Link from "next/link";
import Script from "next/script";
import { LanguageToggle } from "./components/layout/LanguageToggle";
import { ThemeToggle } from "./components/layout/ThemeToggle";
import { resolveLocale } from "./lib/i18n";
import { DEFAULT_THEME, resolveTheme } from "./lib/theme";
import { UiTextProvider } from "./lib/uiTextContext";
import { getUiText } from "./lib/uiTextLocale";

export const metadata: Metadata = {
  icons: {
    icon: "/brand/icon-mark.png",
    apple: "/brand/icon-mark.png",
  },
};

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const cookieStore = await cookies();
  const locale = resolveLocale(cookieStore.get("ui-lang")?.value);
  const theme = resolveTheme(cookieStore.get("ui-theme")?.value) ?? DEFAULT_THEME;
  const uiText = getUiText(locale);

  return (
    <html lang={locale} data-theme={theme} suppressHydrationWarning>
      <head>
        <Script src="/theme-init.js" strategy="beforeInteractive" />
      </head>
      <body className="min-h-screen bg-[var(--md-sys-color-surface-container-high)] text-[var(--md-sys-color-on-surface)]">
        <UiTextProvider locale={locale}>
          <header className="border-b border-[var(--md-sys-color-outline)] bg-[var(--brand-header-bg)] text-[var(--brand-header-fg)]">
            <div className="mx-auto flex max-w-[min(1400px,calc(100%-2rem))] flex-wrap items-center justify-between gap-3 px-4 py-3">
              <Link href="/dashboard" className="inline-flex items-center gap-2">
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
                <nav className="flex flex-wrap items-center gap-3 text-sm text-[var(--brand-header-fg-muted)]">
                  <Link href="/dashboard" className="hover:text-[var(--brand-header-fg)] hover:underline">
                    {uiText.navigation.dashboard}
                  </Link>
                  <Link href="/definitions" className="hover:text-[var(--brand-header-fg)] hover:underline">
                    {uiText.navigation.definitions}
                  </Link>
                  <Link href="/workflows" className="hover:text-[var(--brand-header-fg)] hover:underline">
                    {uiText.navigation.workflows}
                  </Link>
                </nav>
                <ThemeToggle theme={theme} />
                <LanguageToggle locale={locale} />
              </div>
            </div>
          </header>
          <div className="mx-auto max-w-[min(1400px,calc(100%-2rem))] px-4 py-6">{children}</div>
        </UiTextProvider>
      </body>
    </html>
  );
}
