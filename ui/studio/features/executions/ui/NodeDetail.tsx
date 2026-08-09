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

/** ノード詳細パネル用の派生表示値。 */
type NodeDetailDerivedFields = {
  style: ReturnType<typeof getStatusStyle>;
  canResume: boolean;
  isWaiting: boolean;
  isCanceled: boolean;
  isFailed: boolean;
  nodeNameText: string;
  outputText: string;
  inputText: string;
  conditionRoutingText: string;
  showTracePanel: boolean;
  allowedEventsLabel: string;
  effectiveResumeEvent: string;
};

/**
 * ノード詳細表示用の派生フィールドを計算する。
 */
function deriveNodeDetailFields(
  node: ExecutionNodeDTO,
  resumeDisabledReason: string | null,
  resumeEvents: string[],
  selectedResumeEvent: string
): NodeDetailDerivedFields {
  const selected = selectedResumeEvent.trim();
  return {
    style: getStatusStyle(node.status),
    canResume: !resumeDisabledReason,
    isWaiting: node.status === "WAITING",
    isCanceled: node.status === "CANCELED",
    isFailed: node.status === "FAILED",
    nodeNameText: typeof node.nodeName === "string" ? node.nodeName.trim() : "",
    outputText: "output" in node && node.output !== undefined ? formatTracePayload(node.output) : "",
    inputText: "input" in node && node.input !== undefined ? formatTracePayload(node.input) : "",
    conditionRoutingText:
      "conditionRouting" in node && node.conditionRouting !== undefined
        ? formatTracePayload(node.conditionRouting)
        : "",
    showTracePanel:
      (node.startedAt != null && node.startedAt !== "") ||
      (node.completedAt != null && node.completedAt !== "") ||
      ("input" in node && node.input !== undefined) ||
      ("output" in node && node.output !== undefined) ||
      ("conditionRouting" in node && node.conditionRouting !== undefined),
    allowedEventsLabel: resumeEvents.length > 0 ? resumeEvents.join(", ") : "—",
    effectiveResumeEvent: selected.length > 0 ? selected : (resumeEvents[0] ?? "")
  };
}

type NodeDetailResumeActionsProps = {
  isWaiting: boolean;
  resumeEvents: string[];
  effectiveResumeEvent: string;
  canResume: boolean;
  loading: boolean;
  resumeDisabledReason: string | null;
  onSelectedResumeEventChange: (eventName: string) => void;
  onResume: (eventName: string) => void;
};

/**
 * WAITING ノード向けのイベント選択と Resume 操作 UI。
 */
function NodeDetailResumeActions({
  isWaiting,
  resumeEvents,
  effectiveResumeEvent,
  canResume,
  loading,
  resumeDisabledReason,
  onSelectedResumeEventChange,
  onResume
}: Readonly<NodeDetailResumeActionsProps>) {
  const uiText = useUiText();
  return (
    <div className="mt-3 space-y-2">
      {isWaiting && resumeEvents.length > 1 && (
        <label className="block space-y-1 text-xs text-[var(--md-sys-color-on-surface)]">
          <span>{uiText.nodeDetail.waiting.selectResumeEvent}</span>
          <select
            className="w-full rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] px-3 py-2 text-sm"
            value={effectiveResumeEvent}
            disabled={!canResume || loading}
            onChange={(event) => onSelectedResumeEventChange(event.target.value)}
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
      {resumeDisabledReason && (
        <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">{resumeDisabledReason}</p>
      )}
    </div>
  );
}

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
  }, [node?.nodeId, resumeEventsKey]);

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

  const fields = deriveNodeDetailFields(node, resumeDisabledReason, resumeEvents, selectedResumeEvent);

  return (
    <aside className={asideClassName}>
      <h2 className="text-sm font-semibold">{uiText.nodeDetail.title(uiText.entities.node)}</h2>
      <div className={`mt-3 rounded-xl border p-3 ${fields.style.borderClass} ${fields.style.bgClass}`}>
        <div className="flex items-center justify-between">
          <div className="font-mono text-xs">{uiText.nodeDetail.meta.nodeId(node.nodeId)}</div>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${fields.style.badgeClass}`}>
            {node.status}
          </span>
        </div>
        <div className="mt-2 space-y-1 text-xs text-[var(--md-sys-color-on-surface)]">
          {node.workerId != null && node.workerId !== "" && (
            <div className="font-mono">{uiText.nodeDetail.meta.workerId(node.workerId)}</div>
          )}
          <div>{uiText.nodeDetail.meta.type(node.nodeType)}</div>
          {fields.nodeNameText !== "" && <div>{uiText.nodeDetail.meta.nodeName(fields.nodeNameText)}</div>}
          <div>{uiText.nodeDetail.meta.attempt(node.attempt)}</div>
          <div>{uiText.nodeDetail.meta.waitKey(node.waitKey ?? "—")}</div>
          <div>{uiText.nodeDetail.meta.allowedEvents(fields.allowedEventsLabel)}</div>
          <div>{uiText.nodeDetail.meta.canceledByExecution(node.canceledByExecution)}</div>

          {fields.showTracePanel && (
            <NodeDetailTraceSection
              node={node}
              inputText={fields.inputText}
              outputText={fields.outputText}
              conditionRoutingText={fields.conditionRoutingText}
            />
          )}

          <NodeDetailStatusPanels
            node={node}
            isWaiting={fields.isWaiting}
            isCanceled={fields.isCanceled}
            isFailed={fields.isFailed}
            resumeEventName={resumeEventName}
          />
        </div>
      </div>
      {showResumeAction && (
        <NodeDetailResumeActions
          isWaiting={fields.isWaiting}
          resumeEvents={resumeEvents}
          effectiveResumeEvent={fields.effectiveResumeEvent}
          canResume={fields.canResume}
          loading={loading}
          resumeDisabledReason={resumeDisabledReason}
          onSelectedResumeEventChange={setSelectedResumeEvent}
          onResume={onResume}
        />
      )}
    </aside>
  );
}
