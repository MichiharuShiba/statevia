import type { ExecutionStatus, NodeStatus } from "./types";

export type StatusLike = ExecutionStatus | NodeStatus;

export type StatusStyle = {
  badgeClass: string;
  borderClass: string;
  bgClass: string;
  textClass: string;
  icon: string;
  emphasisRank: number;
};

const STATUS_STYLE: Record<StatusLike, StatusStyle> = {
  ACTIVE: {
    badgeClass: "bg-blue-600 text-white",
    borderClass: "border-blue-300",
    bgClass: "bg-blue-50",
    textClass: "text-blue-900",
    icon: "•",
    emphasisRank: 20
  },
  COMPLETED: {
    badgeClass: "bg-emerald-600 text-white",
    borderClass: "border-emerald-300",
    bgClass: "bg-emerald-50",
    textClass: "text-emerald-900",
    icon: "✓",
    emphasisRank: 40
  },
  FAILED: {
    badgeClass: "bg-red-500 text-white",
    borderClass: "border-red-400",
    bgClass: "bg-red-50",
    textClass: "text-red-900",
    icon: "⚠",
    emphasisRank: 80
  },
  CANCELED: {
    badgeClass: "bg-red-600 text-white",
    borderClass: "border-red-600",
    bgClass: "bg-red-50",
    textClass: "text-red-900",
    icon: "✕",
    emphasisRank: 100
  },
  IDLE: {
    badgeClass: "bg-zinc-300 text-zinc-800",
    borderClass: "border-zinc-200",
    bgClass: "bg-white",
    textClass: "text-zinc-800",
    icon: "○",
    emphasisRank: 10
  },
  READY: {
    badgeClass: "bg-blue-600 text-white",
    borderClass: "border-blue-300",
    bgClass: "bg-blue-50",
    textClass: "text-blue-900",
    icon: "•",
    emphasisRank: 20
  },
  RUNNING: {
    badgeClass: "bg-zinc-200 text-zinc-800",
    borderClass: "border-zinc-200",
    bgClass: "bg-white",
    textClass: "text-zinc-600",
    icon: "▶",
    emphasisRank: 30
  },
  WAITING: {
    badgeClass: "bg-amber-500 text-white",
    borderClass: "border-amber-400",
    bgClass: "bg-amber-50",
    textClass: "text-amber-900",
    icon: "⏸",
    emphasisRank: 70
  },
  SUCCEEDED: {
    badgeClass: "bg-emerald-600 text-white",
    borderClass: "border-emerald-300",
    bgClass: "bg-emerald-50",
    textClass: "text-emerald-900",
    icon: "✓",
    emphasisRank: 40
  }
};

export function getStatusStyle(status: StatusLike): StatusStyle {
  return STATUS_STYLE[status];
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

