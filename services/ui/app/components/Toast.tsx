"use client";

export type ToastState = {
  tone: "error" | "info" | "success";
  message: string;
};

type ToastProps = {
  toast: ToastState | null;
  onClose: () => void;
};

export function Toast({ toast, onClose }: ToastProps) {
  if (!toast) return null;

  const toneClass =
    toast.tone === "error"
      ? "border-red-200 bg-red-50 text-red-900"
      : toast.tone === "success"
        ? "border-emerald-200 bg-emerald-50 text-emerald-900"
        : "border-zinc-200 bg-white text-zinc-900";

  return (
    <div className={`rounded-2xl border px-4 py-3 text-sm ${toneClass}`}>
      <div className="flex items-start justify-between gap-4">
        <p>{toast.message}</p>
        <button className="text-zinc-500 hover:text-zinc-900" onClick={onClose} aria-label="close toast">
          ✕
        </button>
      </div>
    </div>
  );
}

