"use client";

import { useUiText } from "../../lib/uiTextContext";

type ExecutionStatusBannerProps = {
  cancelRequested?: boolean;
  terminal?: boolean;
};

export function ExecutionStatusBanner({ cancelRequested, terminal }: Readonly<ExecutionStatusBannerProps>) {
  const uiText = useUiText();
  if (cancelRequested) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-900">
        {uiText.executionStatusBanner.cancelRequestedNotice(uiText.actions.cancel, uiText.actions.resume)}
      </div>
    );
  }
  if (terminal) {
    return (
      <div className="rounded-xl border border-zinc-300 bg-zinc-100 px-3 py-2 text-xs text-zinc-800">
        {uiText.executionStatusBanner.terminalNotice(uiText.entities.execution)}
      </div>
    );
  }
  return null;
}
