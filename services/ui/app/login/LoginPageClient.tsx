"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { toToastError } from "../lib/errors";
import type { LoginRequestBody } from "../lib/authSession";
import type { ApiError } from "../lib/types";
import { resolveSafeInternalRedirectPath } from "../lib/safeInternalRedirect";
import { useUiText } from "../lib/uiTextContext";

type LoginOk = { ok: true };

type PasswordVisibilityIconProps = Readonly<{
  visible: boolean;
}>;

/** パスワード表示切替ボタン用アイコン（装飾のみ）。 */
function PasswordVisibilityIcon({ visible }: PasswordVisibilityIconProps) {
  if (visible) {
    return (
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth={1.5}
        className="h-5 w-5"
        aria-hidden="true"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          d="M3.98 8.223A10.477 10.477 0 0 0 1.934 12c1.292 2.4 4.286 6 10.066 6 2.137 0 4.1-.52 5.838-1.443M6.228 6.228A10.451 10.451 0 0 1 12 4.5c5.78 0 8.774 3.6 10.066 6a10.523 10.523 0 0 1-4.293 5.774M6.228 6.228 3 3m3.228 3.228 3.65 3.65m7.894 7.894L21 21m-3.228-3.228-3.65-3.65m0 0a3 3 0 1 0-4.243-4.243m4.242 4.242L9.88 9.88"
        />
      </svg>
    );
  }
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      className="h-5 w-5"
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M2.036 12.322a1.012 1.012 0 0 1 0-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178Z"
      />
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
    </svg>
  );
}

/**
 * テナントキー + メール + パスワードでログインし、セッション Cookie を設定する。
 */
export function LoginPageClient() {
  const uiText = useUiText();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [tenantKey, setTenantKey] = useState("default");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const redirectTarget = resolveSafeInternalRedirectPath(searchParams.get("from"));

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setErrorMessage(null);
    setSubmitting(true);

    const body: LoginRequestBody = {
      tenantKey: tenantKey.trim(),
      email: email.trim(),
      password
    };

    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        credentials: "same-origin",
        body: JSON.stringify(body)
      });
      const json: unknown = await res.json().catch(() => null);
      if (!res.ok) {
        const apiError: ApiError = {
          status: res.status,
          error:
            typeof json === "object" && json !== null && "error" in json
              ? (json as ApiError).error
              : { code: `HTTP_${res.status}`, message: res.statusText || uiText.auth.errors.network }
        };
        setErrorMessage(toToastError(apiError).message);
        return;
      }
      if (typeof json === "object" && json !== null && "ok" in json && (json as LoginOk).ok) {
        router.replace(redirectTarget);
        router.refresh();
        return;
      }
      setErrorMessage(uiText.auth.errors.unexpectedResponse);
    } catch {
      setErrorMessage(uiText.auth.errors.network);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-md">
      <h1 className="mb-2 text-2xl font-semibold text-[var(--md-sys-color-on-surface)]">
        {uiText.auth.login.title}
      </h1>
      <p className="mb-6 text-sm text-[var(--md-sys-color-on-surface-variant)]">
        {uiText.auth.login.description}
      </p>

      <form
        onSubmit={(event) => {
          void handleSubmit(event);
        }}
        className="space-y-4 rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-6 shadow-sm"
        noValidate
      >
        <div>
          <label htmlFor="login-tenant-key" className="mb-1 block text-sm font-medium">
            {uiText.auth.login.tenantKeyLabel}
          </label>
          <input
            id="login-tenant-key"
            name="tenantKey"
            type="text"
            autoComplete="organization"
            required
            value={tenantKey}
            onChange={(e) => setTenantKey(e.target.value)}
            className="w-full rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm"
          />
        </div>

        <div>
          <label htmlFor="login-email" className="mb-1 block text-sm font-medium">
            {uiText.auth.login.emailLabel}
          </label>
          <input
            id="login-email"
            name="email"
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm"
          />
        </div>

        <div>
          <label htmlFor="login-password" className="mb-1 block text-sm font-medium">
            {uiText.auth.login.passwordLabel}
          </label>
          <div className="relative">
            <input
              id="login-password"
              name="password"
              type={passwordVisible ? "text" : "password"}
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface-container)] py-2 pl-3 pr-10 text-sm"
            />
            <button
              type="button"
              className="absolute inset-y-0 right-0 flex items-center px-3 text-[var(--md-sys-color-on-surface-variant)] hover:text-[var(--md-sys-color-on-surface)]"
              aria-label={passwordVisible ? uiText.auth.login.hidePassword : uiText.auth.login.showPassword}
              aria-pressed={passwordVisible}
              onClick={() => setPasswordVisible((visible) => !visible)}
            >
              <PasswordVisibilityIcon visible={passwordVisible} />
            </button>
          </div>
        </div>

        {errorMessage ? (
          <p className="text-sm text-red-700" role="alert">
            {errorMessage}
          </p>
        ) : null}

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? uiText.auth.login.submitting : uiText.auth.login.submit}
        </button>
      </form>
    </div>
  );
}
