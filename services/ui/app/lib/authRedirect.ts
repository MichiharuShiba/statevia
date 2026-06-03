type BrowserGlobal = typeof globalThis & {
  location: Location;
};

function hasBrowserLocation(global: typeof globalThis): global is BrowserGlobal {
  return "location" in global && global.location !== undefined;
}

/**
 * ログイン画面 URL を組み立てる（`from` は内部パスのみ）。
 * @param fromPath リダイレクト元パス（クエリ含む可）
 * @param origin オリジン（省略時はブラウザ）
 */
export function buildLoginRedirectUrl(fromPath: string, origin?: string): string {
  let base: string;
  if (origin) {
    base = origin;
  } else {
    base = resolveBrowserOrigin() ?? "http://localhost";
  }
  const url = new URL("/login", base);
  const normalized = fromPath.trim();
  if (
    normalized &&
    normalized.startsWith("/") &&
    !normalized.startsWith("//") &&
    normalized !== "/login"
  ) {
    url.searchParams.set("from", normalized);
  }
  return url.toString();
}

function resolveBrowserOrigin(): string | undefined {
  if (!hasBrowserLocation(globalThis)) {
    return undefined;
  }
  return globalThis.location.origin;
}

/**
 * セッション Cookie を破棄してログイン画面へ遷移する（クライアント専用）。
 */
export async function clearSessionAndRedirectToLogin(): Promise<void> {
  if (!hasBrowserLocation(globalThis)) {
    return;
  }
  const from = `${globalThis.location.pathname}${globalThis.location.search}`;
  try {
    await fetch("/api/auth/logout", { method: "POST", credentials: "same-origin" });
  } catch {
    // ネットワーク失敗時もログインへ誘導する
  }
  globalThis.location.assign(buildLoginRedirectUrl(from));
}
