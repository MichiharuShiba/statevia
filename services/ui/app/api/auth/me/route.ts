import { NextRequest, NextResponse } from "next/server";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "../../../lib/authSession";

function coreApiBase(): string {
  const base = process.env.CORE_API_INTERNAL_BASE;
  if (!base) throw new Error("Missing CORE_API_INTERNAL_BASE");
  return base.replace(/\/$/, "");
}

/**
 * 認証済み Principal 情報（Core-API `GET /v1/auth/me`）をプロキシする。
 */
export async function GET(req: NextRequest) {
  const accessToken = req.cookies.get(AUTH_COOKIE_ACCESS)?.value?.trim();
  const tenantKey = req.cookies.get(AUTH_COOKIE_TENANT_KEY)?.value?.trim();
  if (!accessToken) {
    return NextResponse.json(
      { error: { code: "UNAUTHORIZED", message: "Authentication required." } },
      { status: 401 }
    );
  }

  const headers: Record<string, string> = {
    Accept: "application/json",
    Authorization: `Bearer ${accessToken}`
  };
  if (tenantKey) headers["X-Tenant-Id"] = tenantKey;

  const upstream = await fetch(`${coreApiBase()}/v1/auth/me`, {
    method: "GET",
    headers,
    cache: "no-store"
  });

  const text = await upstream.text();
  let json: unknown = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = { error: { code: "INVALID_RESPONSE", message: "Invalid JSON from auth/me." } };
  }

  return NextResponse.json(json, { status: upstream.status });
}
