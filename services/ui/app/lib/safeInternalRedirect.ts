/** `URL` 解析用の固定オリジン（実際のホストには使わない）。 */
const INTERNAL_REDIRECT_ORIGIN = "http://statevia.internal";

/** ログイン後の既定遷移先。 */
export const DEFAULT_POST_LOGIN_PATH = "/dashboard";

/**
 * ログイン後リダイレクトに使える内部パスか検証し、正規化したパスを返す。
 * 不正・曖昧な入力は `null`（オープンリダイレクト対策）。
 * @param path パス（クエリ・ハッシュ可）
 */
export function normalizeInternalRedirectPath(path: string): string | null {
  const trimmed = path.trim();
  if (!trimmed) {
    return null;
  }
  if (!trimmed.startsWith("/") || trimmed.startsWith("//")) {
    return null;
  }
  if (trimmed.includes("\\")) {
    return null;
  }
  if (containsControlCharacter(trimmed)) {
    return null;
  }

  let parsed: URL;
  try {
    parsed = new URL(trimmed, INTERNAL_REDIRECT_ORIGIN);
  } catch {
    return null;
  }

  if (parsed.origin !== INTERNAL_REDIRECT_ORIGIN) {
    return null;
  }
  if (parsed.username || parsed.password) {
    return null;
  }
  if (parsed.hostname !== "statevia.internal") {
    return null;
  }

  const { pathname } = parsed;
  if (!pathname.startsWith("/") || pathname.startsWith("//")) {
    return null;
  }
  if (/%2f/i.test(pathname)) {
    return null;
  }
  if (pathname.includes(":")) {
    return null;
  }
  if (pathname === "/login" || pathname.startsWith("/login/")) {
    return null;
  }

  return `${pathname}${parsed.search}${parsed.hash}`;
}

/**
 * `from` クエリ等を安全な内部パスに解決する。
 * @param from 未検証の遷移先
 * @param fallback 拒否時の既定パス
 */
export function resolveSafeInternalRedirectPath(
  from: string | null | undefined,
  fallback: string = DEFAULT_POST_LOGIN_PATH
): string {
  if (!from) {
    return fallback;
  }
  return normalizeInternalRedirectPath(from) ?? fallback;
}

function containsControlCharacter(value: string): boolean {
  for (const char of value) {
    const code = char.codePointAt(0) ?? 0;
    if (code <= 0x1f || code === 0x7f) {
      return true;
    }
  }
  return false;
}
