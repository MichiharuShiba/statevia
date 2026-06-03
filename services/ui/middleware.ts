import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import {
  AUTH_COOKIE_ACCESS,
  AUTH_COOKIE_TENANT_KEY,
  hasServerDevAuthBypass
} from "./app/lib/authSession";
import { isAccessTokenSessionValid } from "./app/lib/authTokenValidation";

const PUBLIC_PATHS = ["/login", "/api/auth"];

function isPublicPath(pathname: string): boolean {
  if (PUBLIC_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`))) {
    return true;
  }
  if (pathname.startsWith("/_next") || pathname.startsWith("/brand")) {
    return true;
  }
  if (pathname === "/theme-init.js" || pathname === "/favicon.ico") {
    return true;
  }
  if (/\.[a-z0-9]+$/i.test(pathname)) {
    return true;
  }
  return false;
}

function isAuthenticated(request: NextRequest): boolean {
  if (hasServerDevAuthBypass()) {
    return true;
  }
  const accessToken: string | undefined = request.cookies.get(AUTH_COOKIE_ACCESS)?.value;
  return isAccessTokenSessionValid(accessToken);
}

function appendClearedAuthCookies(response: NextResponse): void {
  response.cookies.delete(AUTH_COOKIE_ACCESS);
  response.cookies.delete(AUTH_COOKIE_TENANT_KEY);
}

function redirectToLogin(request: NextRequest, pathname: string): NextResponse {
  const loginUrl = new URL("/login", request.url);
  if (pathname !== "/" && pathname !== "/login") {
    loginUrl.searchParams.set("from", pathname);
  }
  const response = NextResponse.redirect(loginUrl);
  appendClearedAuthCookies(response);
  return response;
}

/**
 * 未ログイン・期限切れ時は `/login` へ誘導する。`CORE_API_AUTH_TOKEN` 設定時は開発バイパス。
 */
export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (isPublicPath(pathname)) {
    if (pathname === "/login" && isAuthenticated(request)) {
      return NextResponse.redirect(new URL("/dashboard", request.url));
    }
    return NextResponse.next();
  }

  if (isAuthenticated(request)) {
    return NextResponse.next();
  }

  return redirectToLogin(request, pathname);
}

/** Next.js middleware のパスマッチャ。 */
export const config = {
  matcher: ["/((?!_next/static|_next/image).*)"]
};
