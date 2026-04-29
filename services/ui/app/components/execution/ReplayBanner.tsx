"use client";

import { useUiText } from "../../lib/uiTextContext";

type ReplayBannerProps = {
  onBackToCurrent: () => void;
};

export function ReplayBanner({ onBackToCurrent }: Readonly<ReplayBannerProps>) {
  const uiText = useUiText();
  return (
    <div className="rounded-xl border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-900 flex items-center justify-between gap-2 flex-wrap">
      <span>{uiText.executionTimeline.replayingPastStateMessage}</span>
      <button
        type="button"
        onClick={onBackToCurrent}
        className="shrink-0 rounded-lg border border-sky-300 bg-[var(--md-sys-color-surface)] px-2 py-1 font-medium text-sky-800 hover:bg-sky-100"
      >
        {uiText.executionTimeline.backToCurrent}
      </button>
    </div>
  );
}
