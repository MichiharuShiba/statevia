"use client";

import { useEffect, useRef, useState } from "react";

/** Wait `events` 行編集 UI 向けの i18n 文言。 */
export type WaitEventsEditorLabels = {
  waitEventsSectionTitle: string;
  waitEventNameLabel: string;
  waitEventTargetLabel: string;
  waitEventsAdd: string;
  waitEventsRemove: string;
};

/**
 * `WaitEventsEditor` の props。
 *
 * @property events イベント名 → 遷移先ノード名
 * @property labels i18n 文言
 * @property disabled 競合時など編集不可にするとき true
 * @property onEventsChange マップ確定時のコールバック
 */
export type WaitEventsEditorProps = {
  events: Record<string, string>;
  labels: WaitEventsEditorLabels;
  disabled?: boolean;
  onEventsChange: (events: Record<string, string>) => void;
};

type WaitEventRowDraft = {
  id: string;
  eventName: string;
  target: string;
};

/**
 * Wait ノードの `events`（イベント名 → 遷移先）を行編集する。
 *
 * @param props.events 現在のイベントマップ
 * @param props.labels i18n 文言
 * @param props.disabled 競合時など編集不可にするとき true
 * @param props.onEventsChange マップ確定時のコールバック
 */
export function WaitEventsEditor({
  events,
  labels,
  disabled = false,
  onEventsChange
}: Readonly<WaitEventsEditorProps>) {
  const [rows, setRows] = useState<WaitEventRowDraft[]>(() => toRows(events));
  const rowsRef = useRef(rows);
  rowsRef.current = rows;

  useEffect(() => {
    const currentMap = toEvents(rowsRef.current);
    if (sameEventsMap(currentMap, events)) {
      return;
    }
    setRows(toRows(events));
  }, [events]);

  const commitCurrentRows = () => {
    onEventsChange(toEvents(rowsRef.current));
  };

  const patchRow = (rowId: string, patch: Partial<Pick<WaitEventRowDraft, "eventName" | "target">>) => {
    setRows((current) => {
      const next = current.map((entry) => (entry.id === rowId ? { ...entry, ...patch } : entry));
      rowsRef.current = next;
      return next;
    });
  };

  const removeRow = (rowId: string) => {
    const nextRows = rowsRef.current.filter((entry) => entry.id !== rowId);
    setRows(nextRows);
    onEventsChange(toEvents(nextRows));
  };

  const addRow = () => {
    const nextRows = [
      ...rowsRef.current,
      { id: createRowId(), eventName: allocateDraftEventName(rowsRef.current), target: "" }
    ];
    setRows(nextRows);
    onEventsChange(toEvents(nextRows));
  };

  return (
    <div className="space-y-2">
      <p className="text-xs font-medium">{labels.waitEventsSectionTitle}</p>
      {rows.map((row) => (
        <WaitEventRowEditor
          key={row.id}
          row={row}
          labels={labels}
          disabled={disabled}
          onPatch={(patch) => {
            patchRow(row.id, patch);
          }}
          onCommit={commitCurrentRows}
          onRemove={() => {
            removeRow(row.id);
          }}
        />
      ))}
      <button
        type="button"
        className="rounded border border-[var(--md-sys-color-outline-variant)] px-2 py-1 text-xs disabled:opacity-50"
        disabled={disabled}
        onClick={addRow}
      >
        {labels.waitEventsAdd}
      </button>
    </div>
  );
}

type WaitEventRowEditorProps = {
  row: WaitEventRowDraft;
  labels: WaitEventsEditorLabels;
  disabled: boolean;
  onPatch: (patch: Partial<Pick<WaitEventRowDraft, "eventName" | "target">>) => void;
  onCommit: () => void;
  onRemove: () => void;
};

/**
 * Wait `events` の 1 行分の入力 UI。
 *
 * @param props.row 行ドラフト
 * @param props.labels i18n 文言
 * @param props.disabled 編集不可
 * @param props.onPatch フィールド更新
 * @param props.onCommit blur / Enter 時の確定
 * @param props.onRemove 行削除
 */
function WaitEventRowEditor({
  row,
  labels,
  disabled,
  onPatch,
  onCommit,
  onRemove
}: Readonly<WaitEventRowEditorProps>) {
  return (
    <div className="space-y-1 rounded border border-[var(--md-sys-color-outline-variant)] p-2">
      <label className="block text-xs">
        <span className="block">{labels.waitEventNameLabel}</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={row.eventName}
          disabled={disabled}
          onChange={(changeEvent) => {
            onPatch({ eventName: changeEvent.target.value });
          }}
          onBlur={onCommit}
          onKeyDown={(keydownEvent) => {
            if (keydownEvent.key === "Enter") {
              keydownEvent.currentTarget.blur();
            }
          }}
        />
      </label>
      <label className="block text-xs">
        <span className="block">{labels.waitEventTargetLabel}</span>
        <input
          className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
          value={row.target}
          disabled={disabled}
          onChange={(changeEvent) => {
            onPatch({ target: changeEvent.target.value });
          }}
          onBlur={onCommit}
          onKeyDown={(keydownEvent) => {
            if (keydownEvent.key === "Enter") {
              keydownEvent.currentTarget.blur();
            }
          }}
        />
      </label>
      <button
        type="button"
        className="rounded border border-[var(--md-sys-color-outline-variant)] px-2 py-1 text-xs disabled:opacity-50"
        disabled={disabled}
        onClick={onRemove}
      >
        {labels.waitEventsRemove}
      </button>
    </div>
  );
}

function toRows(events: Record<string, string>): WaitEventRowDraft[] {
  return Object.entries(events).map(([eventName, target]) => ({
    id: `${eventName}::${createRowId()}`,
    eventName,
    target
  }));
}

function toEvents(rows: WaitEventRowDraft[]): Record<string, string> {
  return Object.fromEntries(
    rows
      .map((row) => [row.eventName, row.target] as const)
      .filter(([eventName]) => eventName.trim().length > 0)
  );
}

function sameEventsMap(left: Record<string, string>, right: Record<string, string>): boolean {
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  if (leftKeys.length !== rightKeys.length) {
    return false;
  }
  return leftKeys.every((key) => left[key] === right[key]);
}

function allocateDraftEventName(rows: WaitEventRowDraft[]): string {
  const used = new Set(rows.map((row) => row.eventName));
  let suffix = 1;
  let key = `event${suffix}`;
  while (used.has(key)) {
    suffix += 1;
    key = `event${suffix}`;
  }
  return key;
}

function createRowId(): string {
  return crypto.randomUUID();
}
