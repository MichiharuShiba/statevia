import { NextRequest, NextResponse } from "next/server";
import {
  AUTH_COOKIE_ACCESS,
  AUTH_COOKIE_TENANT_KEY,
  hasServerDevAuthBypass,
  isAccessTokenSessionValid
} from "../../../lib/authSession";

/**
 * クライアント向けセッション概要（トークン本体は返さない）。
 */
export function GET(req: NextRequest) {
  const access = req.cookies.get(AUTH_COOKIE_ACCESS)?.value?.trim();
  const tenantKey = req.cookies.get(AUTH_COOKIE_TENANT_KEY)?.value?.trim() ?? "";
  const authenticated = isAccessTokenSessionValid(access) || hasServerDevAuthBypass();
  return NextResponse.json({
    authenticated,
    tenantKey: tenantKey || (process.env.CORE_API_TENANT_ID?.trim() ?? "")
  });
}
