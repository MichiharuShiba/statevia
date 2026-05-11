"use client";

import type { ExecutionNodeDTO, WorkflowView } from "../../lib/types";
import { formatExecutionDuration, formatExecutionInstant } from "../../lib/dateTime";
import { formatTracePayload } from "../../lib/formatExecutionTrace";
import { getStatusStyle } from "../../lib/statusStyle";
import { useLocale, useUiText } from "../../lib/uiTextContext";

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
  const locale = useLocale();
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
          <div className="font-mono text-xs">{node.executionNodeId}</div>
          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${style.badgeClass}`}>
            {node.status}
          </span>
        </div>
        <div className="mt-2 space-y-1 text-xs text-[var(--md-sys-color-on-surface)]">
          <div>{uiText.nodeDetail.meta.type(node.nodeType)}</div>
          {stateNameText !== "" && <div>{uiText.nodeDetail.meta.stateName(stateNameText)}</div>}
          {node.workerId != null && node.workerId !== "" && (
            <div className="font-mono">{uiText.nodeDetail.meta.workerId(node.workerId)}</div>
          )}
          <div>{uiText.nodeDetail.meta.attempt(node.attempt)}</div>
          <div>{uiText.nodeDetail.meta.waitKey(node.waitKey ?? "—")}</div>
          <div>{uiText.nodeDetail.meta.canceledByExecution(node.canceledByExecution)}</div>

          {showTracePanel && (
            <div className="mt-2 space-y-1 border-t border-[var(--md-sys-color-outline-variant)] pt-2">
              {node.startedAt != null && node.startedAt !== "" && (
                <div>{uiText.nodeDetail.trace.startedAt(formatExecutionInstant(node.startedAt, locale))}</div>
              )}
              {node.completedAt != null && node.completedAt !== "" && (
                <div>{uiText.nodeDetail.trace.completedAt(formatExecutionInstant(node.completedAt, locale))}</div>
              )}
              {(() => {
                const durationText = formatExecutionDuration(node.startedAt, node.completedAt);
                if (durationText != null) {
                  return <div>{uiText.nodeDetail.trace.duration(durationText)}</div>;
                }
                if (
                  node.startedAt != null &&
                  node.startedAt !== "" &&
                  node.completedAt != null &&
                  node.completedAt !== ""
                ) {
                  return (
                    <div className="text-[var(--md-sys-color-on-surface-variant)]">
                      {uiText.nodeDetail.trace.durationUnavailable}
                    </div>
                  );
                }
                return null;
              })()}
              {"input" in node && node.input !== undefined && (
                <div>
                  <div className="font-medium text-[var(--md-sys-color-on-surface)]">{uiText.nodeDetail.trace.inputHeading}</div>
                  <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--md-sys-color-surface-container-high)] p-2 text-[10px] leading-snug text-[var(--md-sys-color-on-surface)]">
                    {inputText === "" ? uiText.nodeDetail.trace.inputEmpty : inputText}
                  </pre>
                </div>
              )}
              {"output" in node && node.output !== undefined && (
                <div>
                  <div className="font-medium text-[var(--md-sys-color-on-surface)]">{uiText.nodeDetail.trace.outputHeading}</div>
                  <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--md-sys-color-surface-container-high)] p-2 text-[10px] leading-snug text-[var(--md-sys-color-on-surface)]">
                    {outputText === "" ? uiText.nodeDetail.trace.outputEmpty : outputText}
                  </pre>
                </div>
              )}
              {"conditionRouting" in node && node.conditionRouting !== undefined && (
                <div>
                  <div className="font-medium text-[var(--md-sys-color-on-surface)]">
                    {uiText.nodeDetail.trace.conditionRoutingHeading}
                  </div>
                  <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--md-sys-color-surface-container-high)] p-2 text-[10px] leading-snug text-[var(--md-sys-color-on-surface)]">
                    {conditionRoutingText === "" ? uiText.nodeDetail.trace.conditionRoutingEmpty : conditionRoutingText}
                  </pre>
                </div>
              )}
            </div>
          )}

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
