"use client";

import { useEffect, useState } from "react";
import type { ExecutionNodeDTO, ExecutionView } from "../types";
import { formatTracePayload } from "../lib/formatExecutionTrace";
import { resolveWaitResumeEvents } from "../lib/waitResumeEvents";
import { getStatusStyle } from "@/shared/lib/statusStyle";
import { useUiText } from "@/shared/i18n/uiTextContext";
import { NodeDetailStatusPanels, NodeDetailTraceSection } from "./nodeDetailSections";

type NodeDetailProps = {
  execution: ExecutionView | null;
  node: ExecutionNodeDTO | null;
  loading: boolean;
  /** 選択したイベント名で Resume する。 */
  onResume: (eventName: string) => void;
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
  const resumeEvents = node ? resolveWaitResumeEvents(node) : [];
  const [selectedResumeEvent, setSelectedResumeEvent] = useState(resumeEvents[0] ?? "");
  const resumeEventsKey = resumeEvents.join("\0");

  useEffect(() => {
    const nextEvents = resumeEventsKey.length > 0 ? resumeEventsKey.split("\0") : [];
    setSelectedResumeEvent(nextEvents[0] ?? "");
  }, [node?.executionNodeId, resumeEventsKey]);

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
  const allowedEventsLabel =
    resumeEvents.length > 0 ? resumeEvents.join(", ") : "—";
  const effectiveResumeEvent =
    selectedResumeEvent.trim().length > 0 ? selectedResumeEvent.trim() : (resumeEvents[0] ?? "");

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
          <div>{uiText.nodeDetail.meta.allowedEvents(allowedEventsLabel)}</div>
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
          {isWaiting && resumeEvents.length > 1 && (
            <label className="block space-y-1 text-xs text-[var(--md-sys-color-on-surface)]">
              <span>{uiText.nodeDetail.waiting.selectResumeEvent}</span>
              <select
                className="w-full rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] px-3 py-2 text-sm"
                value={effectiveResumeEvent}
                disabled={!canResume || loading}
                onChange={(event) => setSelectedResumeEvent(event.target.value)}
              >
                {resumeEvents.map((eventName) => (
                  <option key={eventName} value={eventName}>
                    {eventName}
                  </option>
                ))}
              </select>
            </label>
          )}
          <button
            className="w-full rounded-xl bg-amber-500 px-3 py-2 text-sm font-semibold text-white hover:bg-amber-600 disabled:cursor-not-allowed disabled:opacity-50"
            disabled={!canResume || loading || effectiveResumeEvent.length === 0}
            onClick={() => onResume(effectiveResumeEvent)}
          >
            {uiText.actions.resume}
          </button>
          {resumeDisabledReason && <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">{resumeDisabledReason}</p>}
        </div>
      )}
    </aside>
  );
}
