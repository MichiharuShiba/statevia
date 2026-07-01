import { NextResponse } from "next/server";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../../lib/authSession";

/** セッション Cookie を削除する。 */
function clearAuthCookies(res: NextResponse): void {
  res.cookies.delete(AUTH_COOKIE_ACCESS);
  res.cookies.delete(AUTH_COOKIE_TENANT_KEY);
}

/**
 * ログアウト: httpOnly セッション Cookie を削除する。
 */
export function POST() {
  const res = NextResponse.json({ ok: true as const });
  clearAuthCookies(res);
  return res;
}
