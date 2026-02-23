"use client";

type ExecutionStatusBannerProps = {
  cancelRequested?: boolean;
  terminal?: boolean;
};

export function ExecutionStatusBanner({ cancelRequested, terminal }: ExecutionStatusBannerProps) {
  if (cancelRequested) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-900">
        Cancel要求済みのため、Resumeなど進行系操作はできません
      </div>
    );
  }
  if (terminal) {
    return (
      <div className="rounded-xl border border-zinc-300 bg-zinc-100 px-3 py-2 text-xs text-zinc-800">
        Executionは終了しています
      </div>
    );
  }
  return null;
}
