"use client";

import type { ReactNode } from "react";
import type { NodeShapeKind } from "../../lib/nodeAppearance";

type GraphNodeShellProps = {
  shapeKind: NodeShapeKind;
  borderClass: string;
  bgClass: string;
  selected?: boolean;
  diffRing?: string;
  isRunning?: boolean;
  /** 外側コンテナに追記（例: h-full で親いっぱいに広げる） */
  className?: string;
  children: ReactNode;
};

/** フォーク: 上帯（primary） */
const forkBandClass =
  "shrink-0 border-b border-[var(--md-sys-color-outline)]/15 bg-[var(--md-sys-color-primary)]/28 dark:bg-[var(--md-sys-color-primary)]/22";

/** ジョイン: 下帯（secondary） */
const joinBandClass =
  "shrink-0 border-t border-[var(--md-sys-color-outline)]/15 bg-[var(--md-sys-color-secondary)]/28 dark:bg-[var(--md-sys-color-secondary)]/22";

/**
 * ワークフロー／定義グラフ共通のノード外観。
 * フォーク／ジョインは角丸をアクションと同じにし、帯の位置と色で区別する。
 */
export function GraphNodeShell({
  shapeKind,
  borderClass,
  bgClass,
  selected,
  diffRing,
  isRunning,
  className: shellExtraClass,
  children
}: Readonly<GraphNodeShellProps>) {
  const outline = selected ? "outline outline-2 outline-[var(--md-sys-color-primary)] outline-offset-2" : "";
  const running = isRunning ? "opacity-80 text-[var(--md-sys-color-on-surface-variant)]" : "";
  const ring = diffRing ?? "";

  const roundedFrame = `relative overflow-hidden rounded-xl border-2 shadow-sm ${borderClass} ${bgClass} ${running} ${outline} ${ring}`;
  const extra = shellExtraClass?.trim() ? ` ${shellExtraClass.trim()}` : "";

  if (shapeKind === "stadium") {
    return (
      <div className={`rounded-full border-2 px-3 py-3 shadow-sm ${borderClass} ${bgClass} ${running} ${outline} ${ring}${extra}`}>
        {children}
      </div>
    );
  }

  if (shapeKind === "roundedRect") {
    return (
      <div className={`relative rounded-xl border-2 p-3 shadow-sm ${borderClass} ${bgClass} ${running} ${outline} ${ring}${extra}`}>
        {children}
      </div>
    );
  }

  if (shapeKind === "gatewayFork") {
    return (
      <div className={`${roundedFrame} flex min-h-0 flex-1 flex-col${extra}`}>
        <div className={`h-2.5 w-full shrink-0 ${forkBandClass}`} aria-hidden />
        <div className="min-h-0 flex-1 overflow-auto p-3 pt-2.5">{children}</div>
      </div>
    );
  }

  if (shapeKind === "gatewayJoin") {
    return (
      <div className={`${roundedFrame} flex min-h-0 flex-1 flex-col${extra}`}>
        <div className="min-h-0 flex-1 overflow-auto p-3 pb-2.5">{children}</div>
        <div className={`h-2.5 w-full shrink-0 ${joinBandClass}`} aria-hidden />
      </div>
    );
  }

  return (
    <div className={`relative rounded-xl border-2 p-3 shadow-sm ${borderClass} ${bgClass} ${running} ${outline} ${ring}${extra}`}>
      {children}
    </div>
  );
}
