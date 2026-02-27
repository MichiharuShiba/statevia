"use client";

import type { ExecutionEventWithSeq } from "../../lib/types";

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

type ExecutionTimelineProps = {
  events: ExecutionEventWithSeq[];
  loading: boolean;
  error: unknown;
  selectedSeq: number | null;
  onSelectSeq: (seq: number | null) => void;
  onBackToCurrent: () => void;
  isReplaying: boolean;
};

export function ExecutionTimeline({
  events,
  loading,
  error,
  selectedSeq,
  onSelectSeq,
  onBackToCurrent,
  isReplaying
}: Readonly<ExecutionTimelineProps>) {
  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-2 border-b border-zinc-100 pb-2">
        <h2 className="text-sm font-semibold text-zinc-800">実行履歴タイムライン</h2>
        {isReplaying && (
          <button
            type="button"
            onClick={onBackToCurrent}
            className="rounded-lg border border-zinc-300 bg-white px-2 py-1 text-xs font-medium text-zinc-700 hover:bg-zinc-100"
          >
            現在に戻る
          </button>
        )}
      </div>

      {error ? (
        <p className="mt-2 text-xs text-red-600" role="alert">
          {error instanceof Error
            ? error.message
            : typeof error === "object" && error !== null && "message" in error
              ? String((error as { message: unknown }).message)
              : String(error)}
        </p>
      ) : null}

      {loading && (
        <p className="mt-2 text-xs text-zinc-500">読み込み中…</p>
      )}

      {!loading && !error && events.length === 0 && (
        <p className="mt-2 text-xs text-zinc-500">イベントがありません</p>
      )}

      {!loading && events.length > 0 && (
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
      )}
    </section>
  );
}
