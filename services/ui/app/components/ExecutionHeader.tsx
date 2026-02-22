"use client";

import type { ExecutionDTO } from "../lib/types";
import { getStatusStyle } from "../lib/statusStyle";
import type { ViewMode } from "./ViewToggle";
import { ViewToggle } from "./ViewToggle";

type ExecutionHeaderProps = {
  executionId: string;
  onExecutionIdChange: (id: string) => void;
  onLoad: () => void;
  onCancel: () => void;
  loading: boolean;
  canCancel: boolean;
  execution: ExecutionDTO | null;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
};

export function ExecutionHeader({
  executionId,
  onExecutionIdChange,
  onLoad,
  onCancel,
  loading,
  canCancel,
  execution,
  viewMode,
  onViewModeChange
}: ExecutionHeaderProps) {
  const status = execution?.status;
  const style = status ? getStatusStyle(status) : null;

  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex-1">
          <label className="block text-xs font-semibold text-zinc-700">Execution ID</label>
          <input
            className="mt-1 w-full rounded-xl border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400"
            value={executionId}
            onChange={(event) => onExecutionIdChange(event.target.value)}
            placeholder="ex-1"
          />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            className="rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50 disabled:opacity-50"
            onClick={onLoad}
            disabled={loading}
          >
            {loading ? "Loading..." : "Load"}
          </button>
          <button
            className="rounded-xl bg-red-600 px-3 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50"
            onClick={onCancel}
            disabled={!canCancel || loading}
          >
            Cancel
          </button>
          <ViewToggle value={viewMode} onChange={onViewModeChange} />
        </div>
      </div>

      {execution && (
        <div className="mt-4 rounded-xl bg-zinc-50 p-3 text-xs text-zinc-700">
          <div className="flex items-center justify-between">
            <div className="text-sm font-semibold">Execution</div>
            {status && style && (
              <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
                {status}
              </span>
            )}
          </div>
          <div className="mt-2 grid gap-1 sm:grid-cols-2">
            <div>
              graphId: <span className="font-mono">{execution.graphId}</span>
            </div>
            <div>
              cancelRequestedAt: <span className="font-mono">{execution.cancelRequestedAt ?? "—"}</span>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

