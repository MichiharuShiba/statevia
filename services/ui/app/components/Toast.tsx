"use client";

import type { ToastState } from "../lib/errors";
import { useUiText } from "../lib/uiTextContext";

type ToastProps = {
  toast: ToastState | null;
  onClose: () => void;
};

const TOAST_TONE_CLASS_MAP: Record<ToastState["tone"], string> = {
  error: "border-red-200 bg-red-50 text-red-900",
  success: "border-emerald-200 bg-emerald-50 text-emerald-900",
  info: "border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] text-[var(--md-sys-color-on-surface)]"
};

function getToneClass(tone: ToastState["tone"]): string {
  return TOAST_TONE_CLASS_MAP[tone];
}

export function Toast({ toast, onClose }: Readonly<ToastProps>) {
  const uiText = useUiText();
  if (!toast) return null;

  const toneClass = getToneClass(toast.tone);

  return (
    <div
      className={`rounded-2xl border px-4 py-3 text-sm ${toneClass}`}
      role="status"
      aria-live="polite"
      aria-label={toast.message}
    >
      <div className="flex items-start justify-between gap-4">
        <p>{toast.message}</p>
        <button className="text-[var(--md-sys-color-on-surface-variant)] hover:text-[var(--md-sys-color-on-surface)]" onClick={onClose} aria-label={uiText.actions.closeToast}>
          ✕
        </button>
      </div>
    </div>
  );
}

