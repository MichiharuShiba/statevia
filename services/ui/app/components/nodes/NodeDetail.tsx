"use client";

import type { ExecutionNodeDTO, ExecutionView } from "../../lib/types";
import { formatTracePayload } from "../../lib/formatExecutionTrace";
import { getStatusStyle } from "../../lib/statusStyle";
import { useUiText } from "../../lib/uiTextContext";
import { NodeDetailStatusPanels, NodeDetailTraceSection } from "./nodeDetailSections";

type NodeDetailProps = {
  execution: ExecutionView | null;
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

/** 選択ノードの詳細パネル。 */
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
  const stateNameText = typeof node.stateName === "string" ? node.stateName.trim() : "";
  const outputText = "output" in node && node.output !== undefined ? formatTracePayload(node.output) : "";
  const inputText = "input" in node && node.input !== undefined ? formatTracePayload(node.input) : "";
  const conditionRoutingText =
    "conditionRouting" in node && node.conditionRouting !== undefined ? formatTracePayload(node.conditionRouting) : "";
  const showTracePanel =
    (node.startedAt != null && node.startedAt !== "") ||
    (node.completedAt != null && node.completedAt !== "") ||
    ("input" in node && node.input !== undefined) ||
    ("output" in node && node.output !== undefined) ||
    ("conditionRouting" in node && node.conditionRouting !== undefined);

  return (
    <aside className={asideClassName}>
      <h2 className="text-sm font-semibold">{uiText.nodeDetail.title(uiText.entities.node)}</h2>
      <div className={`mt-3 rounded-xl border p-3 ${style.borderClass} ${style.bgClass}`}>
        <div className="flex items-center justify-between">
          <div className="font-mono text-xs">{uiText.nodeDetail.meta.executionNodeId(node.executionNodeId)}</div>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
            {node.status}
          </span>
        </div>
        <div className="mt-2 space-y-1 text-xs text-[var(--md-sys-color-on-surface)]">
          {node.workerId != null && node.workerId !== "" && (
            <div className="font-mono">{uiText.nodeDetail.meta.workerId(node.workerId)}</div>
          )}
          <div>{uiText.nodeDetail.meta.type(node.nodeType)}</div>
          {stateNameText !== "" && <div>{uiText.nodeDetail.meta.stateName(stateNameText)}</div>}
          <div>{uiText.nodeDetail.meta.attempt(node.attempt)}</div>
          <div>{uiText.nodeDetail.meta.waitKey(node.waitKey ?? "—")}</div>
          <div>{uiText.nodeDetail.meta.canceledByExecution(node.canceledByExecution)}</div>

          {showTracePanel && (
            <NodeDetailTraceSection
              node={node}
              inputText={inputText}
              outputText={outputText}
              conditionRoutingText={conditionRoutingText}
            />
          )}

          <NodeDetailStatusPanels
            node={node}
            isWaiting={isWaiting}
            isCanceled={isCanceled}
            isFailed={isFailed}
            resumeEventName={resumeEventName}
          />
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
