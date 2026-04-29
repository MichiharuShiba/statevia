"use client";

import { useEffect, useState } from "react";
import type { Theme } from "../../lib/theme";

type ThemeToggleProps = Readonly<{
  theme: Theme;
}>;

const THEME_COOKIE_KEY = "ui-theme";
const THEME_COOKIE_ATTRS = "Path=/; Max-Age=31536000; SameSite=Lax";

/**
 * 共通ヘッダーで UI テーマ（light / dark）を切り替える。
 */
export function ThemeToggle({ theme }: ThemeToggleProps) {
  const [currentTheme, setCurrentTheme] = useState<Theme>(theme);

  useEffect(() => {
    setCurrentTheme(theme);
  }, [theme]);

  const applyTheme = (nextTheme: Theme) => {
    if (nextTheme === currentTheme) return;
    document.cookie = `${THEME_COOKIE_KEY}=${nextTheme}; ${THEME_COOKIE_ATTRS}`;
    document.documentElement.dataset.theme = nextTheme;
    setCurrentTheme(nextTheme);
  };

  return (
    <div className="inline-flex items-center gap-1 rounded-md border border-white/20 p-1 text-xs">
      <button
        type="button"
        className={`rounded px-2 py-1 ${currentTheme === "light" ? "bg-white/20 text-white" : "text-[var(--brand-header-fg-muted)] hover:text-[var(--brand-header-fg)]"}`}
        onClick={() => applyTheme("light")}
        aria-pressed={currentTheme === "light"}
      >
        Light
      </button>
      <button
        type="button"
        className={`rounded px-2 py-1 ${currentTheme === "dark" ? "bg-white/20 text-white" : "text-[var(--brand-header-fg-muted)] hover:text-[var(--brand-header-fg)]"}`}
        onClick={() => applyTheme("dark")}
        aria-pressed={currentTheme === "dark"}
      >
        Dark
      </button>
    </div>
  );
}
