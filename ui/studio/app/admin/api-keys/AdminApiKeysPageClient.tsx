"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { PageShell } from "../../components/layout/PageShell";
import { PageState } from "../../components/layout/PageState";
import { Toast } from "../../components/Toast";
import { apiDelete, apiGet, apiPost } from "../../lib/api";
import type {
  AdminApiKeyListItem,
  CreatedAdminApiKey,
  PermissionDefinitionDto
} from "../../lib/adminTypes";
import { toToastError, type ToastState } from "../../lib/errors";
import { useUiText } from "../../lib/uiTextContext";

type CreateApiKeyBody = {
  name: string;
  allowedScopes: string[];
  expiresAt?: string;
};

/**
 * チェックボックス用の HTML id（semantic key の `.` 等を置換）。
 */
function checkboxInputId(prefix: string, key: string): string {
  return `${prefix}-${key.replaceAll(/[^a-zA-Z0-9_-]/g, "-")}`;
}

/**
 * ISO 日時をローカル表示用に整形する。
 */
function formatDateTime(value: string | null | undefined): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

/**
 * API キー一覧・発行・失効。
 */
export function AdminApiKeysPageClient() {
  const uiText = useUiText();
  const [apiKeys, setApiKeys] = useState<AdminApiKeyListItem[] | null>(null);
  const [permissions, setPermissions] = useState<PermissionDefinitionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [name, setName] = useState("");
  const [expiresAt, setExpiresAt] = useState("");
  const [selectedScopes, setSelectedScopes] = useState<Set<string>>(new Set());
  const [issuedKey, setIssuedKey] = useState<CreatedAdminApiKey | null>(null);
  const [copied, setCopied] = useState(false);

  const assignablePermissions = useMemo(
    () => permissions.filter((p) => p.permissionKey !== "tenant.admin" && !p.isDeprecated),
    [permissions]
  );

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const [keys, permissionList] = await Promise.all([
        apiGet<AdminApiKeyListItem[]>("/admin/api-keys"),
        apiGet<PermissionDefinitionDto[]>("/admin/permissions")
      ]);
      setApiKeys(keys);
      setPermissions(permissionList);
    } catch (error) {
      setToast(toToastError(error));
      setApiKeys(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function toggleScope(permissionKey: string) {
    setSelectedScopes((prev) => {
      const next = new Set(prev);
      if (next.has(permissionKey)) next.delete(permissionKey);
      else next.add(permissionKey);
      return next;
    });
  }

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setToast(null);
    setCopied(false);
    const body: CreateApiKeyBody = {
      name: name.trim(),
      allowedScopes: [...selectedScopes]
    };
    if (expiresAt.trim()) {
      body.expiresAt = new Date(expiresAt).toISOString();
    }

    try {
      const created = await apiPost<CreatedAdminApiKey>("/admin/api-keys", body);
      setIssuedKey(created);
      setName("");
      setExpiresAt("");
      setSelectedScopes(new Set());
      await load();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleRevoke(apiKey: AdminApiKeyListItem) {
    setRevokingId(apiKey.apiKeyId);
    setToast(null);
    try {
      await apiDelete(`/admin/api-keys/${apiKey.apiKeyId}`);
      await load();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setRevokingId(null);
    }
  }

  async function handleCopyPlainKey() {
    if (!issuedKey) return;
    try {
      await navigator.clipboard.writeText(issuedKey.plainKey);
      setCopied(true);
    } catch {
      setToast({ tone: "error", message: uiText.executionTimeline.errorUnknown });
    }
  }

  return (
    <PageShell title={uiText.admin.apiKeys.title} description={uiText.admin.apiKeys.description}>
      {issuedKey ? (
        <section className="mb-6 rounded-xl border border-amber-500/60 bg-amber-50 p-4 text-sm text-amber-950 shadow-sm dark:bg-amber-950/30 dark:text-amber-100">
          <h2 className="mb-2 text-lg font-medium">{uiText.admin.apiKeys.issuedTitle}</h2>
          <p className="mb-3">{uiText.admin.apiKeys.issuedWarning}</p>
          <label htmlFor="issued-plain-key" className="mb-1 block font-medium">
            {uiText.admin.apiKeys.plainKeyLabel}
          </label>
          <div className="flex flex-wrap items-center gap-2">
            <input
              id="issued-plain-key"
              readOnly
              value={issuedKey.plainKey}
              className="min-w-0 flex-1 rounded-lg border border-[var(--md-sys-color-outline)] bg-white px-3 py-2 font-mono text-xs dark:bg-[var(--md-sys-color-surface)]"
            />
            <button
              type="button"
              onClick={() => {
                void handleCopyPlainKey();
              }}
              className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 hover:bg-[var(--md-sys-color-surface-container)]"
            >
              {copied ? uiText.admin.apiKeys.copiedKey : uiText.admin.apiKeys.copyKey}
            </button>
            <button
              type="button"
              onClick={() => {
                setIssuedKey(null);
                setCopied(false);
              }}
              className="rounded-lg bg-emerald-700 px-3 py-2 text-white hover:bg-emerald-800"
            >
              {uiText.admin.apiKeys.dismissIssued}
            </button>
          </div>
        </section>
      ) : null}

      <form
        onSubmit={(event) => {
          void handleCreate(event);
        }}
        className="rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm"
      >
        <h2 className="mb-3 text-lg font-medium">{uiText.admin.apiKeys.createTitle}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="admin-api-key-name" className="mb-1 block text-sm font-medium">
              {uiText.admin.apiKeys.nameLabel}
            </label>
            <input
              id="admin-api-key-name"
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label htmlFor="admin-api-key-expires" className="mb-1 block text-sm font-medium">
              {uiText.admin.apiKeys.expiresAtLabel}
            </label>
            <input
              id="admin-api-key-expires"
              type="datetime-local"
              value={expiresAt}
              onChange={(e) => setExpiresAt(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
            <p className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
              {uiText.admin.apiKeys.expiresAtHint}
            </p>
          </div>
        </div>
        <fieldset className="mt-4">
          <legend className="mb-2 text-sm font-medium">{uiText.admin.apiKeys.scopesTitle}</legend>
          <div className="grid gap-2 sm:grid-cols-2">
            {assignablePermissions.map((permission) => (
              <label key={permission.permissionKey} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  id={checkboxInputId("api-key-scope", permission.permissionKey)}
                  checked={selectedScopes.has(permission.permissionKey)}
                  onChange={() => toggleScope(permission.permissionKey)}
                />
                {uiText.admin.permissions.label(permission.displayKey, permission.displayLabel)}
              </label>
            ))}
          </div>
        </fieldset>
        <button
          type="submit"
          disabled={submitting || selectedScopes.size === 0}
          className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? uiText.admin.apiKeys.creating : uiText.admin.apiKeys.createSubmit}
        </button>
      </form>

      {renderListBody()}

      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
    </PageShell>
  );

  function renderListBody() {
    if (loading) return <PageState state="loading" />;
    if (apiKeys === null) return <PageState state="error" />;
    if (apiKeys.length === 0) return <PageState state="empty" />;
    return (
      <ul className="divide-y divide-[var(--md-sys-color-outline)] rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)]">
        {apiKeys.map((apiKey) => (
          <li
            key={apiKey.apiKeyId}
            className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
          >
            <div className="min-w-0">
              <p className="font-medium text-[var(--md-sys-color-on-surface)]">{apiKey.name}</p>
              <p className="text-[var(--md-sys-color-on-surface-variant)]">
                {uiText.admin.apiKeys.prefixLabel(apiKey.keyPrefix)}
              </p>
              <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {apiKey.isActive ? uiText.admin.apiKeys.active : uiText.admin.apiKeys.inactive}
                {` · ${apiKey.allowedScopes.join(", ")}`}
              </p>
              <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {uiText.admin.apiKeys.createdLabel(formatDateTime(apiKey.createdAt) ?? apiKey.createdAt)}
                {` · ${
                  apiKey.expiresAt
                    ? uiText.admin.apiKeys.expiresLabel(formatDateTime(apiKey.expiresAt) ?? apiKey.expiresAt)
                    : uiText.admin.apiKeys.noExpiry
                }`}
                {` · ${
                  apiKey.lastUsedAt
                    ? uiText.admin.apiKeys.lastUsedLabel(formatDateTime(apiKey.lastUsedAt) ?? apiKey.lastUsedAt)
                    : uiText.admin.apiKeys.neverUsed
                }`}
              </p>
            </div>
            {apiKey.isActive ? (
              <button
                type="button"
                disabled={revokingId === apiKey.apiKeyId}
                onClick={() => {
                  void handleRevoke(apiKey);
                }}
                className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-1.5 hover:bg-[var(--md-sys-color-surface-container)] disabled:opacity-60"
              >
                {revokingId === apiKey.apiKeyId ? uiText.admin.apiKeys.revoking : uiText.admin.apiKeys.revoke}
              </button>
            ) : null}
          </li>
        ))}
      </ul>
    );
  }
}
