import "./globals.css";
import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { uiText } from "./lib/uiText";

export const metadata: Metadata = {
  icons: {
    icon: "/brand/icon-mark.png",
    apple: "/brand/icon-mark.png",
  },
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ja">
      <body className="min-h-screen bg-[var(--tone-page-bg)] text-[var(--tone-fg-strong)]">
        <header className="border-b border-[var(--tone-border)] bg-[var(--tone-header-bg)] text-[var(--tone-header-fg)]">
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
              <span className="text-[1.75rem] font-semibold tracking-wide leading-none">
                <span>state</span>
                <span className="text-emerald-400">via</span>
              </span>
            </Link>
            <nav className="flex flex-wrap items-center gap-3 text-sm text-[var(--tone-header-fg-muted)]">
              <Link href="/dashboard" className="hover:text-[var(--tone-header-fg)] hover:underline">
                {uiText.navigation.dashboard}
              </Link>
              <Link href="/definitions" className="hover:text-[var(--tone-header-fg)] hover:underline">
                {uiText.navigation.definitions}
              </Link>
              <Link href="/workflows" className="hover:text-[var(--tone-header-fg)] hover:underline">
                {uiText.navigation.workflows}
              </Link>
            </nav>
          </div>
        </header>
        <div className="mx-auto max-w-[min(1400px,calc(100%-2rem))] px-4 py-6">{children}</div>
      </body>
    </html>
  );
}
