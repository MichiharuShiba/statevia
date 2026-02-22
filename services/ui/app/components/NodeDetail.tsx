"use client";

import type { ExecutionDTO, ExecutionNodeDTO } from "../lib/types";
import { getStatusStyle } from "../lib/statusStyle";

type NodeDetailProps = {
  execution: ExecutionDTO | null;
  node: ExecutionNodeDTO | null;
  loading: boolean;
  onResume: () => void;
  resumeDisabledReason: string | null;
  className?: string;
};

export function NodeDetail({ execution, node, loading, onResume, resumeDisabledReason, className }: NodeDetailProps) {
  const baseClassName = "rounded-2xl border border-zinc-200 bg-white p-4 shadow-sm";
  const asideClassName = className ? `${baseClassName} ${className}` : baseClassName;

  if (!execution) {
    return (
      <aside className={asideClassName}>
        <p className="text-sm text-zinc-600">Execution を読み込んでください。</p>
      </aside>
    );
  }

  if (!node) {
    return (
      <aside className={asideClassName}>
        <p className="text-sm text-zinc-600">Node を選択してください。</p>
      </aside>
    );
  }

  const style = getStatusStyle(node.status);
  const canResume = !resumeDisabledReason;

  return (
    <aside className={asideClassName}>
      <h2 className="text-sm font-semibold">Node Detail</h2>
      <div className={`mt-3 rounded-xl border p-3 ${style.borderClass} ${style.bgClass}`}>
        <div className="flex items-center justify-between">
          <div className="font-mono text-xs">{node.nodeId}</div>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
            {node.status}
          </span>
        </div>
        <div className="mt-2 space-y-1 text-xs text-zinc-700">
          <div>type: {node.nodeType}</div>
          <div>attempt: {node.attempt}</div>
          <div>waitKey: {node.waitKey ?? "—"}</div>
          <div>canceledByExecution: {String(node.canceledByExecution)}</div>
          {node.canceledByExecution && <div className="rounded bg-red-100 px-2 py-1 text-red-800">Execution Cancel により収束</div>}
        </div>
      </div>
      <div className="mt-3 space-y-2">
        <button
          className="w-full rounded-xl bg-amber-500 px-3 py-2 text-sm font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
          disabled={!canResume || loading}
          onClick={onResume}
        >
          Resume
        </button>
        {resumeDisabledReason && <p className="text-xs text-zinc-600">{resumeDisabledReason}</p>}
      </div>
    </aside>
  );
}

