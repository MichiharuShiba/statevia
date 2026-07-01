"use client";

import { useId, useMemo, type FocusEvent } from "react";
import { filterActionSchemaCandidates } from "../../lib/actionSchema/filterActionSchemaCandidates";
import type { ActionSchemaIndexItem } from "../../lib/actionSchema/types";

/** ActionId 入力 combobox の props。 */
export type ActionIdComboboxProps = {
  /** 現在の入力値（draft）。 */
  value: string;
  /** Schema index 候補。 */
  candidates: ReadonlyArray<ActionSchemaIndexItem>;
  /** 候補読込中か。 */
  loading?: boolean;
  /** 表示ラベル。 */
  labels: {
    loading: string;
    noResults: string;
  };
  /** 入力変更（draft）。 */
  onChange: (nextValue: string) => void;
  /** 確定（blur / 候補選択）。 */
  onCommit: () => void;
};

/**
 * ActionId を一覧から選ぶかフリー入力できる combobox。
 * 入力に応じて候補をリアルタイム絞り込みする。
 */
export function ActionIdCombobox({
  value,
  candidates,
  loading = false,
  labels,
  onChange,
  onCommit
}: Readonly<ActionIdComboboxProps>) {
  const listId = useId();

  const filteredCandidates = useMemo(
    () => filterActionSchemaCandidates(candidates, value),
    [candidates, value]
  );

  const handleBlur = (_event: FocusEvent<HTMLInputElement>) => {
    onCommit();
  };

  const handleChange = (nextValue: string) => {
    onChange(nextValue);
    if (candidates.some((candidate) => candidate.actionId === nextValue)) {
      onCommit();
    }
  };

  const showNoResults = !loading && value.trim().length > 0 && filteredCandidates.length === 0;

  return (
    <div className="relative">
      <input
        className="mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1"
        list={loading ? undefined : listId}
        value={value}
        disabled={loading}
        onChange={(event) => {
          handleChange(event.target.value);
        }}
        onBlur={handleBlur}
      />
      {loading ? (
        <p className="mt-1 text-[10px] text-[var(--md-sys-color-on-surface-variant)]">{labels.loading}</p>
      ) : (
        <>
          <datalist id={listId}>
            {filteredCandidates.map((item) => (
              <option key={item.actionId} value={item.actionId} label={item.displayName} />
            ))}
          </datalist>
          {showNoResults ? (
            <p className="mt-1 text-[10px] text-[var(--md-sys-color-on-surface-variant)]">{labels.noResults}</p>
          ) : null}
        </>
      )}
    </div>
  );
}
