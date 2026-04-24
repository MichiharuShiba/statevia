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
  const shellClassName = className
    ? `mx-auto flex w-full max-w-5xl flex-col gap-6 p-6 ${className}`
    : "mx-auto flex w-full max-w-5xl flex-col gap-6 p-6";

  return (
    <div className={shellClassName}>
      <header className="flex flex-wrap items-start justify-between gap-4 rounded-2xl border border-[var(--tone-border)] bg-[var(--tone-surface-bg)] px-5 py-4">
        <div className="min-w-0 flex-1">
          <h1 className="text-xl font-semibold text-[var(--tone-fg-strong)]">{title}</h1>
          {description ? <p className="mt-1 text-sm text-[var(--tone-fg-muted)]">{description}</p> : null}
        </div>
        {primaryActions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{primaryActions}</div> : null}
      </header>

      <section className="flex flex-col gap-4">{children}</section>

      {secondaryActions ? (
        <footer className="flex flex-wrap items-center gap-3 border-t border-[var(--tone-border)] pt-3 text-sm text-[var(--tone-fg-muted)]">
          {secondaryActions}
        </footer>
      ) : null}
    </div>
  );
}
