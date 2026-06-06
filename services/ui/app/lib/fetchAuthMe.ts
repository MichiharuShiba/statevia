import type { AuthMeResponse } from "./adminTypes";

/**
 * 現在セッションの Principal 情報を取得する（未認証時は null）。
 */
export async function fetchAuthMe(): Promise<AuthMeResponse | null> {
  const res = await fetch("/api/auth/me", {
    credentials: "same-origin",
    cache: "no-store",
    headers: { Accept: "application/json" }
  });
  if (!res.ok) return null;
  const json: unknown = await res.json().catch(() => null);
  if (typeof json !== "object" || json === null || !("isTenantAdmin" in json)) return null;
  return json as AuthMeResponse;
}
