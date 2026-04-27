"use client";

import { useState } from "react";
import type { ExecutionEventWithSeq } from "../../lib/types";
import { uiText } from "../../lib/uiText";

function formatAt(at: string | undefined): string {
  if (!at) return "—";
  try {
    const d = new Date(at);
    return d.toLocaleString(undefined, {
      month: "numeric",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit"
    });
  } catch {
    return at;
  }
}

function eventLabel(event: ExecutionEventWithSeq): string {
  switch (event.type) {
    case "GraphUpdated":
      return `GraphUpdated (${event.patch?.nodes?.length ?? 0} nodes)`;
    case "ExecutionStatusChanged":
      return `Status → ${event.to}`;
    case "NodeCancelled":
      return `NodeCancelled: ${event.nodeId}`;
    case "NodeFailed":
      return `NodeFailed: ${event.nodeId}`;
    default: {
      const e = event as { type?: string };
      return e.type ?? "Unknown";
    }
  }
}

function toErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  if (typeof error === "string") return error;
  if (typeof error === "object" && error !== null && "message" in error) {
    return String((error as { message: unknown }).message);
  }
  return uiText.executionTimeline.errorUnknown;
}

type ExecutionTimelineProps = {
  events: ExecutionEventWithSeq[];
  loading: boolean;
  error: unknown;
  selectedSeq: number | null;
  onSelectSeq: (seq: number | null) => void;
  onBackToCurrent: () => void;
  isReplaying: boolean;
  hasMore?: boolean;
  loadingMore?: boolean;
  onLoadMore?: () => void;
};

export function ExecutionTimeline({
  events,
  loading,
  error,
  selectedSeq,
  onSelectSeq,
  onBackToCurrent,
  isReplaying,
  hasMore = false,
  loadingMore = false,
  onLoadMore
}: Readonly<ExecutionTimelineProps>) {
  const [expanded, setExpanded] = useState(false);

  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-2 border-b border-zinc-100 pb-2">
        <button
          type="button"
          onClick={() => setExpanded((e) => !e)}
          className="flex flex-1 cursor-pointer items-center gap-1.5 rounded-t-lg -mx-1 px-1 py-0.5 text-left hover:bg-zinc-50/80"
          aria-expanded={expanded}
          aria-controls="execution-timeline-body"
          id="execution-timeline-heading"
        >
          <span
            className={`text-zinc-400 transition-transform ${expanded ? "rotate-90" : ""}`}
            aria-hidden
          >
            ▶
          </span>
          <h2 id="execution-timeline-heading-text" className="text-sm font-semibold text-zinc-800">
            {uiText.executionTimeline.title}
          </h2>
        </button>
        {isReplaying && (
          <button
            type="button"
            onClick={onBackToCurrent}
            className="rounded-lg border border-zinc-300 bg-white px-2 py-1 text-xs font-medium text-zinc-700 hover:bg-zinc-100 shrink-0"
          >
            {uiText.executionTimeline.backToCurrent}
          </button>
        )}
      </div>

      <div
        id="execution-timeline-body"
        aria-labelledby="execution-timeline-heading-text"
        hidden={!expanded}
        className="mt-2"
      >
      {error ? (
        <p className="mt-2 text-xs text-red-600" role="alert">
          {toErrorMessage(error)}
        </p>
      ) : null}

      {loading && (
        <p className="mt-2 text-xs text-zinc-500">{uiText.actions.loading}</p>
      )}

      {!loading && !error && events.length === 0 && (
        <p className="mt-2 text-xs text-zinc-500">{uiText.executionTimeline.empty}</p>
      )}

      {!loading && events.length > 0 && (
        <>
          <ul className="mt-2 max-h-[280px] overflow-y-auto space-y-1">
            {events.map((ev) => {
              const isSelected = selectedSeq === ev.seq;
              return (
                <li key={ev.seq}>
                  <button
                    type="button"
                    onClick={() => onSelectSeq(isSelected ? null : ev.seq)}
                    className={`w-full rounded-lg border px-2 py-1.5 text-left text-xs transition-colors ${
                      isSelected
                        ? "border-zinc-400 bg-zinc-100 font-medium text-zinc-900"
                        : "border-transparent text-zinc-600 hover:bg-zinc-50 hover:text-zinc-800"
                    }`}
                  >
                    <span className="font-mono text-zinc-500">#{ev.seq}</span>
                    <span className="ml-2">{formatAt(ev.at)}</span>
                    <span className="ml-2">{eventLabel(ev)}</span>
                  </button>
                </li>
              );
            })}
          </ul>
          {hasMore && onLoadMore && (
            <div className="mt-2">
              <button
                type="button"
                onClick={onLoadMore}
                disabled={loadingMore}
                className="w-full rounded-lg border border-zinc-200 bg-zinc-50 px-2 py-1.5 text-xs text-zinc-600 hover:bg-zinc-100 disabled:opacity-50"
              >
                {loadingMore ? uiText.actions.loading : uiText.executionTimeline.loadMore}
              </button>
            </div>
          )}
        </>
      )}
      </div>
    </section>
  );
}
