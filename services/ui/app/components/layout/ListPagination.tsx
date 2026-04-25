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
  prevLabel = "前へ",
  nextLabel = "次へ",
  className
}: Readonly<ListPaginationProps>) {
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
        className="rounded border border-zinc-300 bg-white px-3 py-1.5 text-zinc-700 hover:bg-zinc-50 disabled:cursor-not-allowed disabled:opacity-50"
        onClick={onPrev}
        disabled={!hasPrev}
      >
        {prevLabel}
      </button>
      <span className="text-zinc-600">{currentPageLabel}</span>
      <button
        type="button"
        className="rounded border border-zinc-300 bg-white px-3 py-1.5 text-zinc-700 hover:bg-zinc-50 disabled:cursor-not-allowed disabled:opacity-50"
        onClick={onNext}
        disabled={!hasNext}
      >
        {nextLabel}
      </button>
    </nav>
  );
}
