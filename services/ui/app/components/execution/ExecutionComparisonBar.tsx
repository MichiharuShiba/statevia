"use client";

import type { WorkflowView } from "../../lib/types";
import type { ExecutionDiffResult, NodeDiffItem } from "../../lib/executionDiff";
import { getStatusStyle } from "../../lib/statusStyle";
import { useUiText } from "../../lib/uiTextContext";

type ExecutionComparisonBarProps = {
  executionLeft: WorkflowView | null;
  executionRight: WorkflowView | null;
  executionIdRight: string;
  onExecutionIdRightChange: (id: string) => void;
  onLoadRight: () => void;
  loadingRight: boolean;
  diff: ExecutionDiffResult | null;
  onSelectDiffNode?: (nodeId: string) => void;
};

function DiffRow({
  item,
  onSelect
}: Readonly<{
  item: NodeDiffItem;
  onSelect?: (nodeId: string) => void;
}>) {
  const uiText = useUiText();
  const styleLeft = item.statusLeft ? getStatusStyle(item.statusLeft) : null;
  const styleRight = item.statusRight ? getStatusStyle(item.statusRight) : null;
  let label: string;
  if (item.kind === "only_in_left") label = uiText.executionComparison.kind.onlyLeft;
  else if (item.kind === "only_in_right") label = uiText.executionComparison.kind.onlyRight;
  else label = uiText.executionComparison.kind.diff;

  return (
    <button
      type="button"
      className="flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs hover:bg-[var(--md-sys-color-surface-container-high)]"
      onClick={() => onSelect?.(item.nodeId)}
    >
      <span
        className={
          item.isFailureOrCancel
            ? "font-semibold text-red-700"
            : "font-medium text-[var(--md-sys-color-on-surface)]"
        }
      >
        {item.nodeId}
      </span>
      <span className="text-[var(--md-sys-color-on-surface-variant)]">{label}</span>
      {item.statusLeft != null && styleLeft && (
        <span
          className={`inline-flex rounded px-1.5 py-0.5 text-[10px] font-semibold ${styleLeft.badgeClass}`}
        >
          A: {item.statusLeft}
        </span>
      )}
      {item.statusRight != null && styleRight && (
        <span
          className={`inline-flex rounded px-1.5 py-0.5 text-[10px] font-semibold ${styleRight.badgeClass}`}
        >
          B: {item.statusRight}
        </span>
      )}
    </button>
  );
}

export function ExecutionComparisonBar({
  executionLeft,
  executionRight,
  executionIdRight,
  onExecutionIdRightChange,
  onLoadRight,
  loadingRight,
  diff,
  onSelectDiffNode
}: Readonly<ExecutionComparisonBarProps>) {
  const uiText = useUiText();
  const failureOrCancelDiffs = diff?.nodeDiffs.filter((d) => d.isFailureOrCancel) ?? [];
  const otherDiffs = diff?.nodeDiffs.filter((d) => !d.isFailureOrCancel) ?? [];

  return (
    <section className="rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm">
      <div className="mb-3 text-sm font-semibold text-[var(--md-sys-color-on-surface)]">{uiText.executionComparison.title}</div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div>
          <div className="text-xs font-semibold text-[var(--md-sys-color-on-surface-variant)]">
            {uiText.executionComparison.executionABaselineLabel(uiText.entities.execution)}
          </div>
          {executionLeft ? (
            <div className="mt-1 rounded-lg bg-[var(--md-sys-color-surface-container)] px-2 py-1.5 font-mono text-xs text-[var(--md-sys-color-on-surface)]">
              {executionLeft.displayId}
              <span className="ml-2 text-[var(--md-sys-color-on-surface-variant)]">({executionLeft.status})</span>
            </div>
          ) : (
            <div className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">{uiText.executionComparison.state.notLoaded}</div>
          )}
        </div>
        <div>
          <label htmlFor="execution-b-id" className="block text-xs font-semibold text-[var(--md-sys-color-on-surface-variant)]">
            {uiText.executionComparison.executionBLabel(uiText.entities.execution)}
          </label>
          <div className="mt-1 flex gap-2">
            <input
              id="execution-b-id"
              className="flex-1 rounded-lg border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-2 py-1.5 font-mono text-xs text-[var(--md-sys-color-on-surface)] outline-none focus:border-[var(--md-sys-color-primary)]"
              value={executionIdRight}
              onChange={(e) => onExecutionIdRightChange(e.target.value)}
              placeholder={uiText.executionComparison.executionIdPlaceholder}
            />
            <button
              type="button"
              className="rounded-lg border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-xs font-medium text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)] disabled:opacity-50"
              onClick={onLoadRight}
              disabled={loadingRight}
            >
              {loadingRight ? uiText.actions.loading : uiText.actions.load}
            </button>
          </div>
          {executionRight && (
            <div className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
              {executionRight.displayId} ({executionRight.status})
            </div>
          )}
        </div>
        <div className="sm:col-span-2 lg:col-span-1">
          <div className="text-xs font-semibold text-[var(--md-sys-color-on-surface-variant)]">{uiText.executionComparison.summary.title}</div>
          {diff ? (
            <div className="mt-1 max-h-48 overflow-y-auto rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface-container)]/60">
              {failureOrCancelDiffs.length > 0 && (
                <div className="border-b border-[var(--md-sys-color-outline)] px-2 py-1.5">
                  <div className="text-[10px] font-semibold uppercase tracking-wide text-red-600">
                    {uiText.executionComparison.summary.failedOrCancelled}
                  </div>
                  {failureOrCancelDiffs.map((item) => (
                    <DiffRow
                      key={item.nodeId}
                      item={item}
                      onSelect={onSelectDiffNode}
                    />
                  ))}
                </div>
              )}
              {otherDiffs.length > 0 && (
                <div className="px-2 py-1.5">
                  <div className="text-[10px] font-semibold uppercase tracking-wide text-[var(--md-sys-color-on-surface-variant)]">
                    {uiText.executionComparison.summary.others}
                  </div>
                  {otherDiffs.map((item) => (
                    <DiffRow
                      key={item.nodeId}
                      item={item}
                      onSelect={onSelectDiffNode}
                    />
                  ))}
                </div>
              )}
              {diff.nodeDiffs.length === 0 && (
                <div className="px-2 py-3 text-center text-xs text-[var(--md-sys-color-on-surface-variant)]">
                  {uiText.executionComparison.summary.noDiff}
                </div>
              )}
            </div>
          ) : (
            <div className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
              {uiText.executionComparison.summary.loadBothToShow}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
