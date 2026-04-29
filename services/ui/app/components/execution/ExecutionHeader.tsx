"use client";

import type { WorkflowView } from "../../lib/types";
import { useUiText } from "../../lib/uiTextContext";
import { StatusBadge } from "../common/StatusBadge";
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
  /** false のとき Cancel ボタンを表示しない。 */
  showCancelAction?: boolean;
  /** false のとき ViewToggle を表示しない。 */
  showViewToggle?: boolean;
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
  executionIdEditable = true,
  showCancelAction = true,
  showViewToggle = true
}: Readonly<ExecutionHeaderProps>) {
  const uiText = useUiText();
  const status = execution?.status;

  return (
    <section className="rounded-2xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex-1">
          {executionIdEditable ? (
            <>
              <label htmlFor="execution-id-input" className="block text-xs font-semibold text-[var(--md-sys-color-on-surface)]">
                {uiText.executionHeader.executionIdLabel(uiText.entities.execution)}
              </label>
              <input
                id="execution-id-input"
                className="mt-1 w-full rounded-xl border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)] outline-none focus:border-[var(--md-sys-color-primary)]"
                value={executionId}
                onChange={(event) => onExecutionIdChange(event.target.value)}
                placeholder={uiText.executionHeader.placeholderExecutionId}
              />
            </>
          ) : (
            <>
              <span className="block text-xs font-semibold text-[var(--md-sys-color-on-surface)]">
                {uiText.executionHeader.executionIdLabel(uiText.entities.execution)}
              </span>
              <p className="mt-1 rounded-xl border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 font-mono text-sm text-[var(--md-sys-color-on-surface)]">
                {executionId}
              </p>
            </>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {executionIdEditable && (
            <button
              className="rounded-xl border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)] disabled:opacity-50"
              onClick={onLoad}
              disabled={loading}
            >
              {loading ? uiText.actions.loading : uiText.actions.load}
            </button>
          )}
          {showCancelAction && (
            <button
              className="rounded-xl bg-red-600 px-3 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50"
              onClick={onCancel}
              disabled={!canCancel || loading}
            >
              {uiText.actions.cancel}
            </button>
          )}
          {onCompareModeChange && (
            <label className="flex cursor-pointer items-center gap-2 text-sm text-[var(--md-sys-color-on-surface)]">
              <input
                type="checkbox"
                checked={compareMode}
                onChange={(e) => onCompareModeChange(e.target.checked)}
                className="rounded border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]"
              />
              <span>{uiText.executionHeader.compareLabel}</span>
            </label>
          )}
          {onStreamEnabledChange && (
            <label className="flex cursor-pointer items-center gap-2 text-sm text-[var(--md-sys-color-on-surface)]">
              <input
                type="checkbox"
                checked={streamEnabled}
                onChange={(e) => onStreamEnabledChange(e.target.checked)}
                className="rounded border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)]"
              />
              <span>{uiText.executionHeader.realtimeSseLabel}</span>
            </label>
          )}
          {showViewToggle && <ViewToggle value={viewMode} onChange={onViewModeChange} />}
        </div>
      </div>

      {execution && (
        <div className="mt-4 rounded-xl bg-[var(--md-sys-color-surface-container)] p-3 text-xs text-[var(--md-sys-color-on-surface)]">
          <div className="flex items-center justify-between">
            <div className="text-sm font-semibold">{uiText.entities.execution}</div>
            {status && (
              <StatusBadge status={status} className="rounded-full px-2 py-0.5 font-semibold" />
            )}
          </div>
          <div className="mt-2 grid gap-1 sm:grid-cols-2">
            <div>
              <span className="font-mono">
                {uiText.executionHeader.graphIdLine(uiText.labels.graphId, execution.graphId)}
              </span>
            </div>
            <div>
              <span className="font-mono">
                {uiText.executionHeader.cancelRequestedLine(
                  uiText.executionHeader.cancelRequestedLabel,
                  execution.cancelRequested
                )}
              </span>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
