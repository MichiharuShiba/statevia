"use client";

import type { NodeStatus } from "../../lib/types";
import { getStatusStyle } from "../../lib/statusStyle";

const NODE_STATUSES: NodeStatus[] = [
  "IDLE",
  "READY",
  "RUNNING",
  "WAITING",
  "SUCCEEDED",
  "FAILED",
  "CANCELED"
];

const EDGE_ITEMS: { type: string; label: string; stroke: string; strokeWidth: number; dash?: string }[] = [
  { type: "Next", label: "Next", stroke: "#d4d4d8", strokeWidth: 1.2 },
  { type: "Resume", label: "Resume", stroke: "#78716c", strokeWidth: 1.2, dash: "8 4" },
  { type: "Cancel", label: "Cancel", stroke: "#b91c1c", strokeWidth: 2.5 }
];

export function GraphLegend() {
  return (
    <section
      aria-label="グラフ凡例"
      className="absolute bottom-3 left-3 z-10 flex max-h-[min(50vh,280px)] flex-col gap-3 overflow-auto rounded-xl border border-zinc-200 bg-white/95 px-3 py-2.5 shadow-md backdrop-blur-sm sm:bottom-4 sm:left-4 sm:gap-4 sm:px-4 sm:py-3"
    >
      <section aria-label="ノードステータス凡例" className="flex flex-col gap-1">
        <h3 className="text-[9px] font-semibold uppercase tracking-wide text-zinc-500">
          ノードステータス
        </h3>
        <ul className="flex flex-wrap gap-1">
          {NODE_STATUSES.map((status) => {
            const style = getStatusStyle(status);
            return (
              <li key={status} className="flex items-center gap-1">
                <span
                  className={`inline-block h-3 w-3 shrink-0 rounded border sm:h-3.5 sm:w-3.5 ${style.borderClass} ${style.bgClass}`}
                  aria-hidden
                />
                <span className={`rounded px-1 py-0.5 text-[9px] font-semibold ${style.badgeClass}`}>
                  {status}
                </span>
              </li>
            );
          })}
        </ul>
      </section>
      <section aria-label="エッジ種別凡例" className="flex flex-col gap-1">
        <h3 className="text-[9px] font-semibold uppercase tracking-wide text-zinc-500">
          エッジ種別
        </h3>
        <ul className="flex flex-wrap items-center gap-1">
          {EDGE_ITEMS.map((item) => (
            <li key={item.type} className="flex items-center gap-1">
              <svg
                width={28}
                height={10}
                className="shrink-0"
                aria-hidden
              >
                <line
                  x1={0}
                  y1={5}
                  x2={28}
                  y2={5}
                  stroke={item.stroke}
                  strokeWidth={item.strokeWidth}
                  strokeDasharray={item.dash}
                />
                <polygon
                  points="24,1 28,5 24,9"
                  fill={item.stroke}
                />
              </svg>
              <span className="text-[9px] font-medium text-zinc-700">{item.label}</span>
            </li>
          ))}
        </ul>
      </section>
    </section>
  );
}
