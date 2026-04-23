"use client";

import type { WorkflowView } from "../../lib/types";
import { getStatusStyle } from "../../lib/statusStyle";
import type { ViewMode } from "../ViewToggle";
import { ViewToggle } from "../ViewToggle";

type ExecutionHeaderProps = {
  executionId: string;
  onExecutionIdChange: (id: string) => void;
  onLoad: () => void;
  onCancel: () => void;
  loading: boolean;
  canCancel: boolean;
  execution: WorkflowView | null;
  viewMode: ViewMode;
  onViewModeChange: (mode: ViewMode) => void;
  /** 比較モード（2実行差分表示） */
  compareMode?: boolean;
  onCompareModeChange?: (enabled: boolean) => void;
  /** true のとき EventSource で更新。`onStreamEnabledChange` とセットで指定。 */
  streamEnabled?: boolean;
  onStreamEnabledChange?: (enabled: boolean) => void;
  /** false のとき executionId の手入力を無効化し、表示のみ行う。 */
  executionIdEditable?: boolean;
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
  onViewModeChange,
  compareMode = false,
  onCompareModeChange,
  streamEnabled = true,
  onStreamEnabledChange,
  executionIdEditable = true
}: Readonly<ExecutionHeaderProps>) {
  const status = execution?.status;
  const style = status ? getStatusStyle(status) : null;

  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex-1">
          {executionIdEditable ? (
            <>
              <label htmlFor="execution-id-input" className="block text-xs font-semibold text-zinc-700">
                Execution ID
              </label>
              <input
                id="execution-id-input"
                className="mt-1 w-full rounded-xl border border-zinc-200 px-3 py-2 text-sm outline-none focus:border-zinc-400"
                value={executionId}
                onChange={(event) => onExecutionIdChange(event.target.value)}
                placeholder="ex-1"
              />
            </>
          ) : (
            <>
              <span className="block text-xs font-semibold text-zinc-700">Execution ID</span>
              <p className="mt-1 rounded-xl border border-zinc-200 bg-zinc-50 px-3 py-2 font-mono text-sm text-zinc-900">
                {executionId}
              </p>
            </>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {executionIdEditable && (
            <button
              className="rounded-xl border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50 disabled:opacity-50"
              onClick={onLoad}
              disabled={loading}
            >
              {loading ? "Loading..." : "Load"}
            </button>
          )}
          <button
            className="rounded-xl bg-red-600 px-3 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50"
            onClick={onCancel}
            disabled={!canCancel || loading}
          >
            Cancel
          </button>
          {onCompareModeChange && (
            <label className="flex cursor-pointer items-center gap-2 text-sm text-zinc-700">
              <input
                type="checkbox"
                checked={compareMode}
                onChange={(e) => onCompareModeChange(e.target.checked)}
                className="rounded border-zinc-300"
              />
              <span>比較</span>
            </label>
          )}
          {onStreamEnabledChange && (
            <label className="flex cursor-pointer items-center gap-2 text-sm text-zinc-700">
              <input
                type="checkbox"
                checked={streamEnabled}
                onChange={(e) => onStreamEnabledChange(e.target.checked)}
                className="rounded border-zinc-300"
              />
              <span>リアルタイム更新（SSE）</span>
            </label>
          )}
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
              cancelRequested: <span className="font-mono">{execution.cancelRequested ? "true" : "false"}</span>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
