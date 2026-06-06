import { cookies } from "next/headers";
import { AUTH_COOKIE_ACCESS, AUTH_COOKIE_TENANT_KEY } from "./authSession";
import type { AuthMeResponse } from "./adminTypes";

function coreApiBase(): string {
  const base = process.env.CORE_API_INTERNAL_BASE;
  if (!base) throw new Error("Missing CORE_API_INTERNAL_BASE");
  return base.replace(/\/$/, "");
}

/**
 * サーバー側で `GET /v1/auth/me` を呼び、Principal 情報を返す。
 */
export async function fetchAuthMeServer(): Promise<AuthMeResponse | null> {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(AUTH_COOKIE_ACCESS)?.value?.trim();
  const tenantKey = cookieStore.get(AUTH_COOKIE_TENANT_KEY)?.value?.trim();
  if (!accessToken) return null;

  const headers: Record<string, string> = {
    Accept: "application/json",
    Authorization: `Bearer ${accessToken}`
  };
  if (tenantKey) headers["X-Tenant-Id"] = tenantKey;

  const res = await fetch(`${coreApiBase()}/v1/auth/me`, {
    method: "GET",
    headers,
    cache: "no-store"
  });
  if (!res.ok) return null;
  const json: unknown = await res.json().catch(() => null);
  if (typeof json !== "object" || json === null || !("isTenantAdmin" in json)) return null;
  return json as AuthMeResponse;
}
