"use client";

import type { NodeStatus } from "../../lib/types";
import { getStatusStyle } from "../../lib/statusStyle";
import { useUiText } from "../../lib/uiTextContext";

const NODE_STATUSES: NodeStatus[] = [
  "IDLE",
  "READY",
  "RUNNING",
  "WAITING",
  "SUCCEEDED",
  "FAILED",
  "CANCELED"
];

export function GraphLegend() {
  const uiText = useUiText();
  const edgeItems: { type: string; label: string; stroke: string; strokeWidth: number; dash?: string }[] = [
    { type: "Next", label: uiText.status.edgeTypeNext, stroke: "#d4d4d8", strokeWidth: 1.2 },
    { type: "Resume", label: uiText.status.edgeTypeResume, stroke: "#78716c", strokeWidth: 1.2, dash: "8 4" },
    { type: "Cancel", label: uiText.status.edgeTypeCancel, stroke: "#b91c1c", strokeWidth: 2.5 }
  ];

  return (
    <section
      aria-label={uiText.graphLegend.aria.root}
      className="absolute bottom-3 left-3 z-10 flex max-h-[min(50vh,280px)] flex-col gap-3 overflow-auto rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)]/95 px-3 py-2.5 shadow-md backdrop-blur-sm sm:bottom-4 sm:left-4 sm:gap-4 sm:px-4 sm:py-3"
    >
      <section aria-label={uiText.graphLegend.aria.nodeStatus} className="flex flex-col gap-1">
        <h3 className="text-[9px] font-semibold uppercase tracking-wide text-[var(--md-sys-color-on-surface-variant)]">
          {uiText.graphLegend.heading.nodeStatus}
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
      <section aria-label={uiText.graphLegend.aria.edgeType} className="flex flex-col gap-1">
        <h3 className="text-[9px] font-semibold uppercase tracking-wide text-[var(--md-sys-color-on-surface-variant)]">
          {uiText.graphLegend.heading.edgeType}
        </h3>
        <ul className="flex flex-wrap items-center gap-1">
          {edgeItems.map((item) => (
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
              <span className="text-[9px] font-medium text-[var(--md-sys-color-on-surface)]">{item.label}</span>
            </li>
          ))}
        </ul>
      </section>
    </section>
  );
}
