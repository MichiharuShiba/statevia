import { NextRequest, NextResponse } from "next/server";
import {
  AUTH_COOKIE_ACCESS,
  AUTH_COOKIE_TENANT_KEY,
  cookieMaxAgeSeconds,
  type LoginRequestBody,
  type LoginResponseBody
} from "../../../lib/authSession";

function coreApiBase(): string {
  const base = process.env.CORE_API_INTERNAL_BASE;
  if (!base) throw new Error("Missing CORE_API_INTERNAL_BASE");
  return base.replace(/\/$/, "");
}

function cookieOptions(maxAgeSec: number) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: maxAgeSec
  };
}

/**
 * UI ログイン: Core-API `POST /v1/auth/login` を呼び、JWT を httpOnly Cookie に保存する。
 */
export async function POST(req: NextRequest) {
  let body: LoginRequestBody;
  try {
    body = (await req.json()) as LoginRequestBody;
  } catch {
    return NextResponse.json(
      { error: { code: "INVALID_INPUT", message: "Invalid JSON body." } },
      { status: 422 }
    );
  }

  const tenantKey = body.tenantKey?.trim() ?? "";
  const email = body.email?.trim() ?? "";
  const password = body.password ?? "";
  if (!tenantKey || !email || !password) {
    return NextResponse.json(
      { error: { code: "INVALID_INPUT", message: "tenantKey, email, and password are required." } },
      { status: 422 }
    );
  }

  const upstream = await fetch(`${coreApiBase()}/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ tenantKey, email, password }),
    cache: "no-store"
  });

  const text = await upstream.text();
  let json: unknown = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = text;
  }

  if (!upstream.ok) {
    return NextResponse.json(json, { status: upstream.status });
  }

  const login = json as LoginResponseBody;
  if (!login.accessToken?.trim() || !login.tenantKey?.trim() || !login.expiresAt) {
    return NextResponse.json(
      { error: { code: "INVALID_RESPONSE", message: "Login response is incomplete." } },
      { status: 502 }
    );
  }

  const maxAge = cookieMaxAgeSeconds(login.expiresAt);
  const res = NextResponse.json({ ok: true as const });
  res.cookies.set(AUTH_COOKIE_ACCESS, login.accessToken, cookieOptions(maxAge));
  res.cookies.set(AUTH_COOKIE_TENANT_KEY, login.tenantKey, cookieOptions(maxAge));
  return res;
}
