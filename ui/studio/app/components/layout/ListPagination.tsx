"use client";

import { useUiText } from "../../lib/uiTextContext";

type ListPaginationProps = {
  currentPageLabel: string;
  hasPrev: boolean;
  hasNext: boolean;
  onPrev: () => void;
  onNext: () => void;
  ariaLabel: string;
  prevLabel?: string;
  nextLabel?: string;
  className?: string;
};

/**
 * 一覧画面で共通利用するページング操作。
 */
export function ListPagination({
  currentPageLabel,
  hasPrev,
  hasNext,
  onPrev,
  onNext,
  ariaLabel,
  prevLabel,
  nextLabel,
  className
}: Readonly<ListPaginationProps>) {
  const uiText = useUiText();
  const effectivePrevLabel = prevLabel ?? uiText.pagination.prev;
  const effectiveNextLabel = nextLabel ?? uiText.pagination.next;
  const navClassName = [
    "flex items-center gap-2 text-sm",
    className
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <nav className={navClassName} aria-label={ariaLabel}>
      <button
        type="button"
        className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)] disabled:cursor-not-allowed disabled:opacity-50"
        onClick={onPrev}
        disabled={!hasPrev}
      >
        {effectivePrevLabel}
      </button>
      <span className="text-[var(--md-sys-color-on-surface-variant)]">{currentPageLabel}</span>
      <button
        type="button"
        className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)] disabled:cursor-not-allowed disabled:opacity-50"
        onClick={onNext}
        disabled={!hasNext}
      >
        {effectiveNextLabel}
      </button>
    </nav>
  );
}
