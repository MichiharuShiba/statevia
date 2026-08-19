"use client";

import { useState } from "react";
import { apiPut } from "@/shared/api";
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH, PASSWORD_PATTERN } from "@/shared/auth/userIdentity";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { PageShell } from "@/shared/ui/PageShell";
import { Toast } from "@/shared/ui/Toast";

/**
 * 現行パスワード確認後に自分のパスワードを更新する画面。
 */
export function AccountSettingsPageClient() {
  const uiText = useUiText();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [mismatch, setMismatch] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * 確認欄が一致するときだけ本人パスワード更新 API を呼ぶ。
   * @param event フォームの submit イベント
   */
  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      setMismatch(true);
      setToast(null);
      return;
    }

    setMismatch(false);
    setSubmitting(true);
    setToast(null);
    try {
      await apiPut("/auth/me/password", { currentPassword, newPassword });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setToast({ tone: "success", message: uiText.auth.account.success });
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <PageShell title={uiText.auth.account.title} description={uiText.auth.account.description}>
      <form
        onSubmit={(event) => {
          void handleSubmit(event);
        }}
        className="max-w-md space-y-4 rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-6 shadow-sm"
      >
        <div>
          <label htmlFor="account-current-password" className="mb-1 block text-sm font-medium">
            {uiText.auth.account.currentPasswordLabel}
          </label>
          <input
            id="account-current-password"
            type="password"
            autoComplete="current-password"
            required
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label htmlFor="account-new-password" className="mb-1 block text-sm font-medium">
            {uiText.auth.account.newPasswordLabel}
          </label>
          <input
            id="account-new-password"
            type="password"
            autoComplete="new-password"
            required
            minLength={PASSWORD_MIN_LENGTH}
            maxLength={PASSWORD_MAX_LENGTH}
            pattern={PASSWORD_PATTERN}
            title={uiText.auth.account.passwordPolicyHint}
            value={newPassword}
            onChange={(e) => {
              setNewPassword(e.target.value);
              setMismatch(false);
            }}
            className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
          />
          <p className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
            {uiText.auth.account.passwordPolicyHint}
          </p>
        </div>
        <div>
          <label htmlFor="account-confirm-password" className="mb-1 block text-sm font-medium">
            {uiText.auth.account.confirmPasswordLabel}
          </label>
          <input
            id="account-confirm-password"
            type="password"
            autoComplete="new-password"
            required
            minLength={PASSWORD_MIN_LENGTH}
            maxLength={PASSWORD_MAX_LENGTH}
            pattern={PASSWORD_PATTERN}
            title={uiText.auth.account.passwordPolicyHint}
            value={confirmPassword}
            onChange={(e) => {
              setConfirmPassword(e.target.value);
              setMismatch(false);
            }}
            className="w-full rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
          />
        </div>
        {mismatch ? (
          <p className="text-sm text-red-700" role="alert">
            {uiText.auth.account.passwordMismatch}
          </p>
        ) : null}
        <button
          type="submit"
          disabled={submitting}
          className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? uiText.auth.account.submitting : uiText.auth.account.submit}
        </button>
      </form>
      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
    </PageShell>
  );
}
