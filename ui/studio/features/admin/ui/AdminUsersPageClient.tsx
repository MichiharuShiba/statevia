"use client";

import { useCallback, useEffect, useState } from "react";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";
import { apiGet, apiPatch, apiPost, apiPut } from "@/shared/api";
import type { AdminUserListItem } from "../types";
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH, PASSWORD_PATTERN, USER_EMAIL_MAX_LENGTH, USERNAME_MAX_LENGTH, USERNAME_PATTERN } from "@/shared/auth/userIdentity";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { useUiText } from "@/shared/i18n/uiTextContext";

type CreateUserBody = {
  username: string;
  email?: string;
  password: string;
  displayName?: string;
  isTenantAdmin: boolean;
};

/**
 * ユーザー一覧・作成・有効化/無効化・パスワード更新。
 */
export function AdminUsersPageClient() {
  const uiText = useUiText();
  const [users, setUsers] = useState<AdminUserListItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [isTenantAdmin, setIsTenantAdmin] = useState(false);
  const [passwordTarget, setPasswordTarget] = useState<AdminUserListItem | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordMismatch, setPasswordMismatch] = useState(false);
  const [passwordSubmitting, setPasswordSubmitting] = useState(false);

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
    const trimmedEmail = email.trim();
    const body: CreateUserBody = {
      username: username.trim(),
      password,
      isTenantAdmin
    };
    if (trimmedEmail) body.email = trimmedEmail;
    const trimmedDisplay = displayName.trim();
    if (trimmedDisplay) body.displayName = trimmedDisplay;

    try {
      await apiPost<AdminUserListItem>("/admin/users", body);
      setUsername("");
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

  function closePasswordDialog() {
    setPasswordTarget(null);
    setNewPassword("");
    setConfirmPassword("");
    setPasswordMismatch(false);
  }

  function openPasswordDialog(user: AdminUserListItem) {
    setPasswordTarget(user);
    setNewPassword("");
    setConfirmPassword("");
    setPasswordMismatch(false);
    setToast(null);
  }

  /**
   * 確認欄が一致するときだけ管理者パスワード更新 API を呼ぶ。
   * @param event ダイアログの submit イベント
   */
  async function handlePasswordUpdate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!passwordTarget) {
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordMismatch(true);
      return;
    }

    setPasswordMismatch(false);
    setPasswordSubmitting(true);
    setToast(null);
    try {
      await apiPut(`/admin/users/${passwordTarget.userId}/password`, { newPassword });
      closePasswordDialog();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setPasswordSubmitting(false);
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
            <label htmlFor="admin-user-username" className="mb-1 block text-sm font-medium">
              {uiText.admin.users.usernameLabel}
            </label>
            <input
              id="admin-user-username"
              type="text"
              autoComplete="username"
              required
              maxLength={USERNAME_MAX_LENGTH}
              pattern={USERNAME_PATTERN}
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
          </div>
          <div>
            <label htmlFor="admin-user-email" className="mb-1 block text-sm font-medium">
              {uiText.admin.users.emailLabel}
            </label>
            <input
              id="admin-user-email"
              type="email"
              maxLength={USER_EMAIL_MAX_LENGTH}
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
              minLength={PASSWORD_MIN_LENGTH}
              maxLength={PASSWORD_MAX_LENGTH}
              pattern={PASSWORD_PATTERN}
              title={uiText.admin.users.passwordPolicyHint}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
            />
            <p className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
              {uiText.admin.users.passwordPolicyHint}
            </p>
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

      {passwordTarget ? (
        <dialog
          open
          className="fixed inset-0 z-50 m-0 flex h-full max-h-none w-full max-w-none items-center justify-center border-0 bg-black/40 p-4"
          aria-labelledby="admin-password-title"
        >
          <form
            onSubmit={(event) => {
              void handlePasswordUpdate(event);
            }}
            className="w-full max-w-md space-y-4 rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-6 shadow-lg"
          >
            <h2 id="admin-password-title" className="text-lg font-medium">
              {uiText.admin.users.updatePasswordTitle}
            </h2>
            <p className="text-sm text-[var(--md-sys-color-on-surface-variant)]">{passwordTarget.username}</p>
            <div>
              <label htmlFor="admin-user-new-password" className="mb-1 block text-sm font-medium">
                {uiText.admin.users.newPasswordLabel}
              </label>
              <input
                id="admin-user-new-password"
                type="password"
                autoComplete="new-password"
                required
                minLength={PASSWORD_MIN_LENGTH}
                maxLength={PASSWORD_MAX_LENGTH}
                pattern={PASSWORD_PATTERN}
                title={uiText.admin.users.passwordPolicyHint}
                value={newPassword}
                onChange={(e) => {
                  setNewPassword(e.target.value);
                  setPasswordMismatch(false);
                }}
                className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
              />
              <p className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {uiText.admin.users.passwordPolicyHint}
              </p>
            </div>
            <div>
              <label htmlFor="admin-user-confirm-password" className="mb-1 block text-sm font-medium">
                {uiText.admin.users.confirmPasswordLabel}
              </label>
              <input
                id="admin-user-confirm-password"
                type="password"
                autoComplete="new-password"
                required
                minLength={PASSWORD_MIN_LENGTH}
                maxLength={PASSWORD_MAX_LENGTH}
                pattern={PASSWORD_PATTERN}
                title={uiText.admin.users.passwordPolicyHint}
                value={confirmPassword}
                onChange={(e) => {
                  setConfirmPassword(e.target.value);
                  setPasswordMismatch(false);
                }}
                className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
              />
            </div>
            {passwordMismatch ? (
              <p className="text-sm text-red-700" role="alert">
                {uiText.admin.users.passwordMismatch}
              </p>
            ) : null}
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={closePasswordDialog}
                className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-1.5 text-sm hover:bg-[var(--md-sys-color-surface-container)]"
              >
                {uiText.admin.users.cancel}
              </button>
              <button
                type="submit"
                disabled={passwordSubmitting}
                className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
              >
                {passwordSubmitting
                  ? uiText.admin.users.updatingPassword
                  : uiText.admin.users.updatePasswordSubmit}
              </button>
            </div>
          </form>
        </dialog>
      ) : null}

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
              <p className="font-medium text-[var(--md-sys-color-on-surface)]">{user.username}</p>
              <p className="text-[var(--md-sys-color-on-surface-variant)]">
                {user.email ? `${user.displayName} · ${user.email}` : user.displayName}
              </p>
              <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {user.isActive ? uiText.admin.users.active : uiText.admin.users.inactive}
                {user.isTenantAdmin ? ` · ${uiText.admin.users.adminBadge}` : ""}
                {` · ${uiText.admin.users.groupCount(user.groupIds.length)}`}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => {
                  openPasswordDialog(user);
                }}
                className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-1.5 hover:bg-[var(--md-sys-color-surface-container)]"
              >
                {uiText.admin.users.updatePassword}
              </button>
              <button
                type="button"
                onClick={() => {
                  void toggleActive(user);
                }}
                className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-1.5 hover:bg-[var(--md-sys-color-surface-container)]"
              >
                {user.isActive ? uiText.admin.users.disable : uiText.admin.users.enable}
              </button>
            </div>
          </li>
        ))}
      </ul>
    );
  }
}
