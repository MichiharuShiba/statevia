import type { ReactNode } from "react";

type PageShellProps = {
  /** ページの主見出し。 */
  title: string;
  /** 見出し直下の補足文。 */
  description?: string;
  /** 主要導線や操作ボタン。 */
  primaryActions?: ReactNode;
  /** 本文領域。 */
  children: ReactNode;
  /** 戻り導線や補助リンク。 */
  secondaryActions?: ReactNode;
  /** ページ外枠に追加するクラス名。 */
  className?: string;
};

/**
 * 全画面共通のページ骨格。
 * Header / Content / Secondary actions を同じ順序で配置する。
 */
export function PageShell({
  title,
  description,
  primaryActions,
  children,
  secondaryActions,
  className
}: Readonly<PageShellProps>) {
  const shellClassName = [
    "mx-auto flex w-full max-w-5xl flex-col gap-4 p-4 sm:gap-6 sm:p-6",
    className
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={shellClassName}>
      <header className="flex flex-col items-start gap-3 rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] px-4 py-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4 sm:px-5 sm:py-4">
        <div className="min-w-0 flex-1">
          <h1 className="text-xl font-semibold text-[var(--md-sys-color-on-surface)]">{title}</h1>
          {description ? <p className="mt-1 text-sm text-[var(--md-sys-color-on-surface-variant)]">{description}</p> : null}
        </div>
        {primaryActions ? <div className="flex w-full flex-wrap items-center gap-2 sm:w-auto sm:justify-end">{primaryActions}</div> : null}
      </header>

      <section className="flex flex-col gap-4">{children}</section>

      {secondaryActions ? (
        <footer className="flex flex-col items-start gap-2 border-t border-[var(--md-sys-color-outline)] pt-3 text-sm text-[var(--md-sys-color-on-surface-variant)] sm:flex-row sm:items-center sm:gap-3">
          {secondaryActions}
        </footer>
      ) : null}
    </div>
  );
}
