type PageStateKind = "loading" | "empty" | "error";

type PageStateProps = {
  /** 表示する状態種別。 */
  state: PageStateKind;
  /** 補助メッセージ。 */
  message?: string;
  /** 再試行ボタン押下時の処理。error 時のみ有効。 */
  onRetry?: () => void;
  /** 再試行ボタンの文言。 */
  retryLabel?: string;
};

const STATE_STYLE_MAP: Record<PageStateKind, string> = {
  loading: "border-blue-200 bg-blue-50 text-blue-900",
  error: "border-red-200 bg-red-50 text-red-900",
  empty: "border-zinc-300 bg-zinc-50 text-zinc-700"
};

const STATE_TITLE_MAP: Record<PageStateKind, string> = {
  loading: "読み込み中…",
  error: "データの取得に失敗しました。",
  empty: "表示できるデータがありません。"
};

function getStateStyle(state: PageStateKind): string {
  return STATE_STYLE_MAP[state];
}

function getStateTitle(state: PageStateKind): string {
  return STATE_TITLE_MAP[state];
}

/**
 * Loading / Empty / Error の共通表示ブロック。
 */
export function PageState({
  state,
  message,
  onRetry,
  retryLabel = "再試行"
}: Readonly<PageStateProps>) {
  const showRetryButton = state === "error" && typeof onRetry === "function";
  const content = (
    <>
      <p className="font-medium">{getStateTitle(state)}</p>
      {message ? <p className="mt-1">{message}</p> : null}
      {showRetryButton ? (
        <button
          type="button"
          className="mt-3 rounded-md border border-current/30 bg-white/70 px-3 py-1.5 text-sm font-medium hover:bg-white"
          onClick={onRetry}
        >
          {retryLabel}
        </button>
      ) : null}
    </>
  );

  if (state === "error") {
    return (
      <section className={`rounded-2xl border px-4 py-3 text-sm ${getStateStyle(state)}`} role="alert" aria-live="assertive">
        {content}
      </section>
    );
  }

  return (
    <output className={`block rounded-2xl border px-4 py-3 text-sm ${getStateStyle(state)}`} aria-live="polite">
      {content}
    </output>
  );
}
