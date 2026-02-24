"use client";

import { getApiConfig } from "../../lib/api";

/**
 * テナント未指定時に表示するバナー。
 * 認証必須環境では X-Tenant-Id が必要なため、クライアントで未設定の場合は注意を促す。
 */
export function TenantMissingBanner() {
  const { tenantId } = getApiConfig();
  if (tenantId) return null;

  return (
    <div
      className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900"
      role="alert"
    >
      テナントが未指定です。Load / Cancel / Resume が失敗する場合は{" "}
      <code className="mx-1 rounded bg-amber-100 px-1">NEXT_PUBLIC_TENANT_ID</code>
      {" "}を設定するか、サーバーで{" "}
      <code className="mx-1 rounded bg-amber-100 px-1">CORE_API_TENANT_ID</code>
      {" "}を設定してください。
    </div>
  );
}
