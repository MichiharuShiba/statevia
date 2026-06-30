import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { ActionIdCombobox } from "../../../app/components/editor/ActionIdCombobox";
import type { ActionSchemaIndexItem } from "../../../app/lib/actionSchema/types";

const candidates: ActionSchemaIndexItem[] = [
  {
    actionId: "statevia.action.builtin.rest",
    displayName: "REST",
    version: "1.0.0"
  },
  {
    actionId: "statevia.action.builtin.noop",
    displayName: "No-op",
    version: "1.0.0"
  }
];

const labels = {
  loading: "Loading actions…",
  noResults: "No matching actions"
};

function StatefulActionIdCombobox({
  initialValue = "",
  onCommit = vi.fn()
}: Readonly<{ initialValue?: string; onCommit?: () => void }>) {
  const [value, setValue] = useState(initialValue);
  return (
    <ActionIdCombobox
      value={value}
      candidates={candidates}
      labels={labels}
      onChange={setValue}
      onCommit={onCommit}
    />
  );
}

function getDatalistOptions(input: HTMLInputElement): HTMLOptionElement[] {
  const listId = input.getAttribute("list");
  if (!listId) {
    return [];
  }
  const datalist = document.getElementById(listId);
  return datalist ? Array.from(datalist.querySelectorAll("option")) : [];
}

describe("ActionIdCombobox", () => {
  it("入力で候補をリアルタイム絞り込みする", () => {
    render(<StatefulActionIdCombobox />);

    const input = screen.getByRole<HTMLInputElement>("combobox");
    const allOptions = getDatalistOptions(input);
    expect(allOptions.map((option) => option.value)).toEqual([
      "statevia.action.builtin.rest",
      "statevia.action.builtin.noop"
    ]);

    fireEvent.change(input, { target: { value: "rest" } });
    const filteredOptions = getDatalistOptions(input);
    expect(filteredOptions.map((option) => option.value)).toEqual(["statevia.action.builtin.rest"]);
  });

  it("候補選択で actionId を確定する", () => {
    const onCommit = vi.fn();
    render(<StatefulActionIdCombobox onCommit={onCommit} />);

    const input = screen.getByRole("combobox");
    fireEvent.change(input, { target: { value: "statevia.action.builtin.rest" } });

    expect(onCommit).toHaveBeenCalled();
    expect(input).toHaveValue("statevia.action.builtin.rest");
  });

  it("一覧にない文字列もフリー入力で確定できる", () => {
    const onCommit = vi.fn();
    render(<StatefulActionIdCombobox onCommit={onCommit} />);

    const input = screen.getByRole("combobox");
    fireEvent.change(input, { target: { value: "custom.action" } });
    fireEvent.blur(input);

    expect(onCommit).toHaveBeenCalled();
    expect(input).toHaveValue("custom.action");
  });

  it("候補がないとき noResults を表示する", () => {
    render(<StatefulActionIdCombobox />);

    const input = screen.getByRole("combobox");
    fireEvent.change(input, { target: { value: "unknown.action" } });

    expect(screen.getByText(labels.noResults)).toBeInTheDocument();
  });
});
