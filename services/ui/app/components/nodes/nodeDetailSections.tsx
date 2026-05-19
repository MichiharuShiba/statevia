"use client";

import type { ExecutionNodeDTO } from "../../lib/types";
import { formatExecutionDuration, formatExecutionInstant } from "../../lib/dateTime";
import { useLocale, useUiText } from "../../lib/uiTextContext";

type TracePayloadDisclosureProps = {
  heading: string;
  payloadText: string;
  emptyLabel: string;
};

function TracePayloadDisclosure({ heading, payloadText, emptyLabel }: Readonly<TracePayloadDisclosureProps>) {
  const display = payloadText === "" ? emptyLabel : payloadText;
  return (
    <details className="mt-1 rounded-lg border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container-high)]/60">
      <summary className="cursor-pointer select-none list-none px-2 py-1.5 text-xs font-medium text-[var(--md-sys-color-on-surface)] outline-none marker:content-none [&::-webkit-details-marker]:hidden">
        {heading}
      </summary>
      <pre className="mx-2 mb-2 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-[var(--md-sys-color-surface-container-high)] p-2 text-[10px] leading-snug text-[var(--md-sys-color-on-surface)]">
        {display}
      </pre>
    </details>
  );
}

type NodeDetailTraceSectionProps = {
  node: ExecutionNodeDTO;
  inputText: string;
  outputText: string;
  conditionRoutingText: string;
};

function NodeDetailTraceDuration({ node }: Readonly<Pick<NodeDetailTraceSectionProps, "node">>) {
  const uiText = useUiText();
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
}

/** ノード実行トレース（時刻・入出力）を表示する。 */
export function NodeDetailTraceSection({
  node,
  inputText,
  outputText,
  conditionRoutingText
}: Readonly<NodeDetailTraceSectionProps>) {
  const uiText = useUiText();
  const locale = useLocale();
  return (
    <div className="mt-2 space-y-1 border-t border-[var(--md-sys-color-outline-variant)] pt-2">
      {node.startedAt != null && node.startedAt !== "" && (
        <div>{uiText.nodeDetail.trace.startedAt(formatExecutionInstant(node.startedAt, locale))}</div>
      )}
      {node.completedAt != null && node.completedAt !== "" && (
        <div>{uiText.nodeDetail.trace.completedAt(formatExecutionInstant(node.completedAt, locale))}</div>
      )}
      <NodeDetailTraceDuration node={node} />
      {"input" in node && node.input !== undefined && (
        <TracePayloadDisclosure
          heading={uiText.nodeDetail.trace.inputHeading}
          payloadText={inputText}
          emptyLabel={uiText.nodeDetail.trace.inputEmpty}
        />
      )}
      {"output" in node && node.output !== undefined && (
        <TracePayloadDisclosure
          heading={uiText.nodeDetail.trace.outputHeading}
          payloadText={outputText}
          emptyLabel={uiText.nodeDetail.trace.outputEmpty}
        />
      )}
      {"conditionRouting" in node && node.conditionRouting !== undefined && (
        <TracePayloadDisclosure
          heading={uiText.nodeDetail.trace.conditionRoutingHeading}
          payloadText={conditionRoutingText}
          emptyLabel={uiText.nodeDetail.trace.conditionRoutingEmpty}
        />
      )}
    </div>
  );
}

type NodeDetailStatusPanelsProps = {
  node: ExecutionNodeDTO;
  isWaiting: boolean;
  isCanceled: boolean;
  isFailed: boolean;
  resumeEventName?: string | null;
};

/** WAITING / CANCELED / FAILED の状態別詳細を表示する。 */
export function NodeDetailStatusPanels({
  node,
  isWaiting,
  isCanceled,
  isFailed,
  resumeEventName
}: Readonly<NodeDetailStatusPanelsProps>) {
  const uiText = useUiText();
  return (
    <>
      {isWaiting && (
        <div className="mt-2 rounded-lg border border-amber-200 bg-amber-50/80 p-2">
          <div className="font-medium text-amber-900">{uiText.nodeDetail.waiting.title}</div>
          <div className="mt-1 text-amber-800">
            <div>{uiText.nodeDetail.waiting.reasonWaitByWaitKeyAndResumeWait}</div>
            {resumeEventName != null && resumeEventName !== "" && (
              <div className="mt-0.5 font-medium">{uiText.nodeDetail.waiting.resumeEventName(resumeEventName)}</div>
            )}
          </div>
        </div>
      )}
      {isCanceled && (
        <div className="mt-2 rounded-lg border border-red-200 bg-red-50/80 p-2">
          <div className="font-medium text-red-900">{uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel)}</div>
          <div className="mt-1 space-y-0.5 text-red-800">
            {node.cancelReason != null && node.cancelReason !== "" && <div>reason: {node.cancelReason}</div>}
            {node.canceledByExecution && (
              <div className="rounded bg-red-100 px-2 py-1">{uiText.nodeDetail.cancel.convergedByExecutionCancel}</div>
            )}
          </div>
        </div>
      )}
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
    </>
  );
}
