"use client";

import { useCallback, useEffect, useState } from "react";
import { PageShell } from "../../components/layout/PageShell";
import { PageState } from "../../components/layout/PageState";
import { Toast } from "../../components/Toast";
import { apiGet, apiPatch, apiPost } from "../../lib/api";
import type { AdminUserListItem } from "../../lib/adminTypes";
import { toToastError, type ToastState } from "../../lib/errors";
import { useUiText } from "../../lib/uiTextContext";

type CreateUserBody = {
  email: string;
  password: string;
  displayName?: string;
  isTenantAdmin: boolean;
};

/**
 * ユーザー一覧・作成・有効化/無効化。
 */
export function AdminUsersPageClient() {
  const uiText = useUiText();
  const [users, setUsers] = useState<AdminUserListItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [isTenantAdmin, setIsTenantAdmin] = useState(false);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const list = await apiGet<AdminUserListItem[]>("/admin/users");
      setUsers(list);
    } catch (error) {
      setToast(toToastError(error));
      setUsers(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers]);

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setToast(null);
    const body: CreateUserBody = {
      email: email.trim(),
      password,
      isTenantAdmin
    };
    const trimmedDisplay = displayName.trim();
    if (trimmedDisplay) body.displayName = trimmedDisplay;

    try {
      await apiPost<AdminUserListItem>("/admin/users", body);
      setEmail("");
      setPassword("");
      setDisplayName("");
      setIsTenantAdmin(false);
      await loadUsers();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSubmitting(false);
    }
  }

  async function toggleActive(user: AdminUserListItem) {
    setToast(null);
    try {
      await apiPatch<AdminUserListItem>(`/admin/users/${user.userId}`, {
        isActive: !user.isActive
      });
      await loadUsers();
    } catch (error) {
      setToast(toToastError(error));
    }
  }

  return (
    <PageShell title={uiText.admin.users.title} description={uiText.admin.users.description}>
      <form
        onSubmit={(event) => {
          void handleCreate(event);
        }}
        className="rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm"
      >
        <h2 className="mb-3 text-lg font-medium">{uiText.admin.users.createTitle}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="admin-user-email" className="mb-1 block text-sm font-medium">
              {uiText.admin.users.emailLabel}
            </label>
            <input
              id="admin-user-email"
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label htmlFor="admin-user-password" className="mb-1 block text-sm font-medium">
              {uiText.admin.users.passwordLabel}
            </label>
            <input
              id="admin-user-password"
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label htmlFor="admin-user-display" className="mb-1 block text-sm font-medium">
              {uiText.admin.users.displayNameLabel}
            </label>
            <input
              id="admin-user-display"
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
          </div>
          <label className="flex items-center gap-2 self-end text-sm">
            <input
              type="checkbox"
              checked={isTenantAdmin}
              onChange={(e) => setIsTenantAdmin(e.target.checked)}
            />
            {uiText.admin.users.isTenantAdminLabel}
          </label>
        </div>
        <button
          type="submit"
          disabled={submitting}
          className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? uiText.admin.users.creating : uiText.admin.users.createSubmit}
        </button>
      </form>

      {renderListBody()}

      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
    </PageShell>
  );

  function renderListBody() {
    if (loading) return <PageState state="loading" />;
    if (users === null) return <PageState state="error" />;
    if (users.length === 0) return <PageState state="empty" />;
    return (
      <ul className="divide-y divide-[var(--md-sys-color-outline)] rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)]">
        {users.map((user) => (
          <li
            key={user.userId}
            className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
          >
            <div className="min-w-0">
              <p className="font-medium text-[var(--md-sys-color-on-surface)]">{user.email}</p>
              <p className="text-[var(--md-sys-color-on-surface-variant)]">{user.displayName}</p>
              <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {user.isActive ? uiText.admin.users.active : uiText.admin.users.inactive}
                {user.isTenantAdmin ? ` · ${uiText.admin.users.adminBadge}` : ""}
                {` · ${uiText.admin.users.groupCount(user.groupIds.length)}`}
              </p>
            </div>
            <button
              type="button"
              onClick={() => {
                void toggleActive(user);
              }}
              className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-1.5 hover:bg-[var(--md-sys-color-surface-container)]"
            >
              {user.isActive ? uiText.admin.users.disable : uiText.admin.users.enable}
            </button>
          </li>
        ))}
      </ul>
    );
  }
}
