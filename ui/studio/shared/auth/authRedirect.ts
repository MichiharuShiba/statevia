import { normalizeInternalRedirectPath } from "./safeInternalRedirect";

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
  const safeFrom = normalizeInternalRedirectPath(fromPath);
  if (safeFrom) {
    url.searchParams.set("from", safeFrom);
  }
  return url.toString();
}

function resolveBrowserOrigin(): string | undefined {
  if (!hasBrowserLocation(globalThis)) {
    return undefined;
  }
  return globalThis.location.origin;
}

type ClearSessionRedirectOptions = {
  /**
   * 真のとき現在パスをログインの `from` に付ける（401 再認証向け）。
   * 明示ログアウトでは付けない。
   */
  includeFrom?: boolean;
};

/**
 * セッション Cookie を破棄してログイン画面へ遷移する（クライアント専用）。
 * @param options 遷移時に元パスを残すかどうか。省略時は残す
 */
export async function clearSessionAndRedirectToLogin(
  options?: ClearSessionRedirectOptions
): Promise<void> {
  if (!hasBrowserLocation(globalThis)) {
    return;
  }
  const includeFrom = options?.includeFrom !== false;
  const from = includeFrom ? `${globalThis.location.pathname}${globalThis.location.search}` : "";
  try {
    await fetch("/api/auth/logout", { method: "POST", credentials: "same-origin" });
  } catch {
    // ネットワーク失敗時もログインへ誘導する
  }
  globalThis.location.assign(buildLoginRedirectUrl(from));
}
