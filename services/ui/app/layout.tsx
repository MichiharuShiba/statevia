import "./globals.css";
import type { Metadata } from "next";
import { cookies } from "next/headers";
import Script from "next/script";
import { AppHeader } from "./components/layout/AppHeader";
import { resolveLocale } from "./lib/i18n";
import { DEFAULT_THEME, resolveTheme } from "./lib/theme";
import { UiTextProvider } from "./lib/uiTextContext";

/** Next.js のルートメタデータ（ファビコン等）。 */
export const metadata: Metadata = {
  icons: {
    icon: "/brand/icon-mark.png",
    apple: "/brand/icon-mark.png",
  },
};

/**
 * アプリ全体のルートレイアウト。テーマ・ロケール・共通ヘッダを子ページへ提供する。
 */
export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const cookieStore = await cookies();
  const locale = resolveLocale(cookieStore.get("ui-lang")?.value);
  const theme = resolveTheme(cookieStore.get("ui-theme")?.value) ?? DEFAULT_THEME;

  return (
    <html lang={locale} data-theme={theme} suppressHydrationWarning>
      <head>
        <Script src="/theme-init.js" strategy="beforeInteractive" />
      </head>
      <body className="min-h-screen bg-[var(--md-sys-color-surface-container-high)] text-[var(--md-sys-color-on-surface)]">
        <UiTextProvider locale={locale}>
          <AppHeader theme={theme} locale={locale} />
          <div className="mx-auto max-w-[min(1400px,calc(100%-2rem))] px-4 py-6">{children}</div>
        </UiTextProvider>
      </body>
    </html>
  );
}
