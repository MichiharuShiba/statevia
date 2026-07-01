"use client";

import { useEffect, useState } from "react";
import { getApiConfig } from "../../lib/api";
import { useUiText } from "../../lib/uiTextContext";

type SessionSummary = {
  authenticated: boolean;
  tenantKey: string;
};

/**
 * テナント未指定時に表示するバナー。
 * Cookie セッションまたは環境変数でテナントが解決される場合は非表示。
 */
export function TenantMissingBanner() {
  const uiText = useUiText();
  const { tenantId } = getApiConfig();
  const [session, setSession] = useState<SessionSummary | null>(null);
  const [sessionLoaded, setSessionLoaded] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch("/api/auth/session", { credentials: "same-origin" })
      .then((res) => (res.ok ? res.json() : null))
      .then((data: SessionSummary | null) => {
        if (cancelled) return;
        setSession(data ?? { authenticated: false, tenantKey: "" });
        setSessionLoaded(true);
      })
      .catch(() => {
        if (!cancelled) {
          setSession({ authenticated: false, tenantKey: "" });
          setSessionLoaded(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (tenantId) return null;
  if (!sessionLoaded) return null;
  if (session?.authenticated) return null;

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
      <span className="mt-1 block">{uiText.auth.login.sessionHint}</span>
    </div>
  );
}
