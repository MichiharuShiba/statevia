"use client";

import type { ToastState } from "../lib/errors";

type ToastProps = {
  toast: ToastState | null;
  onClose: () => void;
};

function getToneClass(tone: ToastState["tone"]): string {
  if (tone === "error") return "border-red-200 bg-red-50 text-red-900";
  if (tone === "success") return "border-emerald-200 bg-emerald-50 text-emerald-900";
  return "border-zinc-200 bg-white text-zinc-900";
}

export function Toast({ toast, onClose }: Readonly<ToastProps>) {
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
        <button className="text-zinc-500 hover:text-zinc-900" onClick={onClose} aria-label="close toast">
          ✕
        </button>
      </div>
    </div>
  );
}

