import type { NodeStatus, WorkflowStatus } from "./types";

/**
 * ライトはトークンどおり、ダークでは *-container のベタ塗りが濃く見えるため混色で弱める。
 * `var(...)/NN` は hex トークンでは効かないことがあるため `color-mix` を使う。
 * IDLE / RUNNING のニュートラル帯には使わない。
 *
 * クラス名はソースに静的な文字列としてのみ現わす（Tailwind が `` `...${x}...` `` を arbitrary として誤検出するのを避ける）。
 */
const darkSoftContainerBgClass = {
  info: "bg-[var(--md-sys-color-info-container)] dark:bg-[color-mix(in_srgb,var(--md-sys-color-info-container)_30%,transparent)]",
  success:
    "bg-[var(--md-sys-color-success-container)] dark:bg-[color-mix(in_srgb,var(--md-sys-color-success-container)_30%,transparent)]",
  error: "bg-[var(--md-sys-color-error-container)] dark:bg-[color-mix(in_srgb,var(--md-sys-color-error-container)_30%,transparent)]",
  warning:
    "bg-[var(--md-sys-color-warning-container)] dark:bg-[color-mix(in_srgb,var(--md-sys-color-warning-container)_30%,transparent)]"
} as const;

type DarkSoftContainerSemantic = keyof typeof darkSoftContainerBgClass;

/** @param semantic コンテナ色の系統（info / success / error / warning） */
function softDarkContainerBackground(semantic: DarkSoftContainerSemantic): string {
  return darkSoftContainerBgClass[semantic];
}

export type StatusLike = WorkflowStatus | NodeStatus;

export type StatusStyle = {
  badgeClass: string;
  borderClass: string;
  bgClass: string;
  textClass: string;
  icon: string;
  emphasisRank: number;
};

const STATUS_STYLE: Record<StatusLike, StatusStyle> = {
  Running: {
    badgeClass: "bg-[var(--md-sys-color-info)] text-[var(--md-sys-color-on-info)]",
    borderClass: "border-[var(--md-sys-color-info)]",
    bgClass: softDarkContainerBackground("info"),
    textClass: "text-[var(--md-sys-color-on-info-container)]",
    icon: "•",
    emphasisRank: 20
  },
  Completed: {
    badgeClass: "bg-[var(--md-sys-color-success)] text-[var(--md-sys-color-on-success)]",
    borderClass: "border-[var(--md-sys-color-success)]",
    bgClass: softDarkContainerBackground("success"),
    textClass: "text-[var(--md-sys-color-on-success-container)]",
    icon: "✓",
    emphasisRank: 40
  },
  Failed: {
    badgeClass: "bg-[var(--md-sys-color-error)] text-[var(--md-sys-color-on-error)]",
    borderClass: "border-[var(--md-sys-color-error)]",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-[var(--md-sys-color-on-error-container)]",
    icon: "⚠",
    emphasisRank: 80
  },
  Cancelled: {
    badgeClass: "bg-[var(--md-sys-color-error)] text-[var(--md-sys-color-on-error)]",
    borderClass: "border-[var(--md-sys-color-error)]",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-[var(--md-sys-color-on-error-container)]",
    icon: "✕",
    emphasisRank: 100
  },
  IDLE: {
    badgeClass: "bg-[var(--md-sys-color-neutral)] text-[var(--md-sys-color-on-neutral)]",
    borderClass: "border-[var(--md-sys-color-outline-variant)]",
    bgClass: "bg-[var(--md-sys-color-neutral-container)]",
    textClass: "text-[var(--md-sys-color-on-neutral-container)]",
    icon: "○",
    emphasisRank: 10
  },
  READY: {
    badgeClass: "bg-[var(--md-sys-color-info)] text-[var(--md-sys-color-on-info)]",
    borderClass: "border-[var(--md-sys-color-info)]",
    bgClass: softDarkContainerBackground("info"),
    textClass: "text-[var(--md-sys-color-on-info-container)]",
    icon: "•",
    emphasisRank: 20
  },
  RUNNING: {
    badgeClass: "bg-[var(--md-sys-color-neutral)] text-[var(--md-sys-color-on-neutral)]",
    borderClass: "border-[var(--md-sys-color-outline-variant)]",
    bgClass: "bg-[var(--md-sys-color-neutral-container)]",
    textClass: "text-[var(--md-sys-color-on-neutral-container)]",
    icon: "▶",
    emphasisRank: 30
  },
  WAITING: {
    badgeClass: "bg-[var(--md-sys-color-warning)] text-[var(--md-sys-color-on-warning)]",
    borderClass: "border-[var(--md-sys-color-warning)]",
    bgClass: softDarkContainerBackground("warning"),
    textClass: "text-[var(--md-sys-color-on-warning-container)]",
    icon: "⏸",
    emphasisRank: 70
  },
  SUCCEEDED: {
    badgeClass: "bg-[var(--md-sys-color-success)] text-[var(--md-sys-color-on-success)]",
    borderClass: "border-[var(--md-sys-color-success)]",
    bgClass: softDarkContainerBackground("success"),
    textClass: "text-[var(--md-sys-color-on-success-container)]",
    icon: "✓",
    emphasisRank: 40
  },
  FAILED: {
    badgeClass: "bg-[var(--md-sys-color-error)] text-[var(--md-sys-color-on-error)]",
    borderClass: "border-[var(--md-sys-color-error)]",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-[var(--md-sys-color-on-error-container)]",
    icon: "⚠",
    emphasisRank: 80
  },
  CANCELED: {
    badgeClass: "bg-[var(--md-sys-color-error)] text-[var(--md-sys-color-on-error)]",
    borderClass: "border-[var(--md-sys-color-error)]",
    bgClass: softDarkContainerBackground("error"),
    textClass: "text-[var(--md-sys-color-on-error-container)]",
    icon: "✕",
    emphasisRank: 100
  }
};

const DEFAULT_STYLE: StatusStyle = {
  badgeClass: "bg-[var(--md-sys-color-neutral)] text-[var(--md-sys-color-on-neutral)]",
  borderClass: "border-[var(--md-sys-color-outline-variant)]",
  bgClass: "bg-[var(--md-sys-color-neutral-container)]",
  textClass: "text-[var(--md-sys-color-on-neutral-container)]",
  icon: "○",
  emphasisRank: 0
};

export function getStatusStyle(status: StatusLike): StatusStyle {
  return STATUS_STYLE[status] ?? DEFAULT_STYLE;
}

export function getNodeSortWeight(status: NodeStatus): number {
  const order: Record<NodeStatus, number> = {
    WAITING: 1,
    CANCELED: 2,
    FAILED: 3,
    RUNNING: 4,
    READY: 5,
    SUCCEEDED: 6,
    IDLE: 7
  };
  return order[status];
}

