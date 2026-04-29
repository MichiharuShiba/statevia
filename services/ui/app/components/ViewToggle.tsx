"use client";

import { useUiText } from "../lib/uiTextContext";

export type ViewMode = "list" | "graph";

type ViewToggleProps = {
  value: ViewMode;
  onChange: (mode: ViewMode) => void;
};

export function ViewToggle({ value, onChange }: Readonly<ViewToggleProps>) {
  const uiText = useUiText();
  return (
    <div className="inline-flex rounded-xl border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] p-1">
      <button
        className={`rounded-lg px-3 py-1.5 text-sm ${value === "list" ? "border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] text-[var(--brand-cta-fg)]" : "text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)]"}`}
        onClick={() => onChange("list")}
      >
        {uiText.actions.viewList}
      </button>
      <button
        className={`rounded-lg px-3 py-1.5 text-sm ${value === "graph" ? "border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] text-[var(--brand-cta-fg)]" : "text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)]"}`}
        onClick={() => onChange("graph")}
      >
        {uiText.actions.viewGraph}
      </button>
    </div>
  );
}

