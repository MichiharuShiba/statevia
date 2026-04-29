"use client";

import type { ExecutionNodeDTO, WorkflowView } from "../../lib/types";
import { getStatusStyle } from "../../lib/statusStyle";
import { useUiText } from "../../lib/uiTextContext";

type NodeDetailProps = {
  execution: WorkflowView | null;
  node: ExecutionNodeDTO | null;
  loading: boolean;
  onResume: () => void;
  resumeDisabledReason: string | null;
  /** WAITING ノードの Resume に必要なイベント名（グラフ定義の Resume エッジから取得） */
  resumeEventName?: string | null;
  /** false のとき Resume ボタンを表示しない。 */
  showResumeAction?: boolean;
  className?: string;
};

export function NodeDetail({
  execution,
  node,
  loading,
  onResume,
  resumeDisabledReason,
  resumeEventName,
  showResumeAction = true,
  className
}: Readonly<NodeDetailProps>) {
  const uiText = useUiText();
  const baseClassName = "rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm";
  const asideClassName = className ? `${baseClassName} ${className}` : baseClassName;

  if (!execution) {
    return (
      <aside className={asideClassName}>
        <p className="text-sm text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeDetail.prompts.loadExecution(uiText.entities.execution)}</p>
      </aside>
    );
  }

  if (!node) {
    return (
      <aside className={asideClassName}>
        <p className="text-sm text-[var(--md-sys-color-on-surface-variant)]">{uiText.nodeDetail.prompts.selectNode(uiText.entities.node)}</p>
      </aside>
    );
  }

  const style = getStatusStyle(node.status);
  const canResume = !resumeDisabledReason;
  const isWaiting = node.status === "WAITING";
  const isCanceled = node.status === "CANCELED";
  const isFailed = node.status === "FAILED";

  return (
    <aside className={asideClassName}>
      <h2 className="text-sm font-semibold">{uiText.nodeDetail.title(uiText.entities.node)}</h2>
      <div className={`mt-3 rounded-xl border p-3 ${style.borderClass} ${style.bgClass}`}>
        <div className="flex items-center justify-between">
          <div className="font-mono text-xs">{node.nodeId}</div>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
            {node.status}
          </span>
        </div>
        <div className="mt-2 space-y-1 text-xs text-[var(--md-sys-color-on-surface)]">
          <div>{uiText.nodeDetail.meta.type(node.nodeType)}</div>
          <div>{uiText.nodeDetail.meta.attempt(node.attempt)}</div>
          <div>{uiText.nodeDetail.meta.waitKey(node.waitKey ?? "—")}</div>
          <div>{uiText.nodeDetail.meta.canceledByExecution(node.canceledByExecution)}</div>

          {/* Wait / Resume 詳細 */}
          {isWaiting && (
            <div className="mt-2 rounded-lg border border-amber-200 bg-amber-50/80 p-2">
              <div className="font-medium text-amber-900">{uiText.nodeDetail.waiting.title}</div>
              <div className="mt-1 text-amber-800">
                <div>{uiText.nodeDetail.waiting.reasonWaitByWaitKeyAndResumeWait}</div>
                {resumeEventName != null && resumeEventName !== "" && (
                  <div className="mt-0.5 font-medium">
                    {uiText.nodeDetail.waiting.resumeEventName(resumeEventName)}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Cancel 詳細 */}
          {isCanceled && (
            <div className="mt-2 rounded-lg border border-red-200 bg-red-50/80 p-2">
              <div className="font-medium text-red-900">{uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel)}</div>
              <div className="mt-1 space-y-0.5 text-red-800">
                {node.cancelReason != null && node.cancelReason !== "" && (
                  <div>reason: {node.cancelReason}</div>
                )}
                {node.canceledByExecution && (
                  <div className="rounded bg-red-100 px-2 py-1">
                    {uiText.nodeDetail.cancel.convergedByExecutionCancel}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* 失敗情報 */}
          {isFailed && (
            <div className="mt-2 rounded-lg border border-red-300 bg-red-50 p-2">
              <div className="font-medium text-red-900">{uiText.nodeDetail.failure.title}</div>
              <div className="mt-1 text-red-800">
                {node.error?.message != null && node.error.message !== "" ? (
                  <div className="break-words">{node.error.message}</div>
                ) : (
                  <div className="text-red-600">{uiText.nodeDetail.failure.noMessage}</div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
      {showResumeAction && (
        <div className="mt-3 space-y-2">
          <button
            className="w-full rounded-xl bg-amber-500 px-3 py-2 text-sm font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
            disabled={!canResume || loading}
            onClick={onResume}
          >
            {uiText.actions.resume}
          </button>
          {resumeDisabledReason && <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">{resumeDisabledReason}</p>}
        </div>
      )}
    </aside>
  );
}
