"use client";

import { getApiConfig } from "../../lib/api";
import { uiText } from "../../lib/uiText";

/**
 * テナント未指定時に表示するバナー。
 * 認証必須環境では X-Tenant-Id が必要なため、クライアントで未設定の場合は注意を促す。
 */
export function TenantMissingBanner() {
  const { tenantId } = getApiConfig();
  if (tenantId) return null;
  const noticeParts = uiText.tenantMissingBanner.noticeParts(
    uiText.actions.load,
    uiText.actions.cancel,
    uiText.actions.resume
  );

  return (
    <div
      className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900"
      role="alert"
    >
      {noticeParts.beforePrimaryEnv}
      <code className="mx-1 rounded bg-amber-100 px-1">NEXT_PUBLIC_TENANT_ID</code>
      {noticeParts.betweenEnvs}
      <code className="mx-1 rounded bg-amber-100 px-1">CORE_API_TENANT_ID</code>
      {noticeParts.afterSecondaryEnv}
    </div>
  );
}
