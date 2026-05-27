import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { ExecutionHeader } from "../../../app/components/execution/ExecutionHeader";
import { renderWithUiText } from "../../testUtils";
import type { ExecutionView } from "../../../app/lib/types";

function executionViewFixture(overrides: Partial<ExecutionView> = {}): ExecutionView {
  return {
    displayId: "ex-1",
    resourceId: "wf-1",
    graphId: "g-1",
    status: "Running",
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    nodes: [],
    ...overrides
  };
}

describe("ExecutionHeader", () => {
  it("Load / Cancel を操作できる", () => {
    const onLoad = vi.fn();
    const onCancel = vi.fn();
    const onExecutionIdChange = vi.fn();

    renderWithUiText(
      <ExecutionHeader
        executionId="ex-1"
        onExecutionIdChange={onExecutionIdChange}
        onLoad={onLoad}
        onCancel={onCancel}
        loading={false}
        canCancel
        execution={executionViewFixture()}
        viewMode="graph"
        onViewModeChange={vi.fn()}
      />
    );

    fireEvent.change(screen.getByLabelText(/実行 ID/i), { target: { value: "ex-2" } });
    expect(onExecutionIdChange).toHaveBeenCalledWith("ex-2");

    fireEvent.click(screen.getByRole("button", { name: "ロード" }));
    expect(onLoad).toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "キャンセル" }));
    expect(onCancel).toHaveBeenCalled();
  });

  it("executionIdEditable=false のとき入力欄は表示のみ", () => {
    renderWithUiText(
      <ExecutionHeader
        executionId="ex-readonly"
        onExecutionIdChange={vi.fn()}
        onLoad={vi.fn()}
        onCancel={vi.fn()}
        loading={false}
        canCancel={false}
        execution={null}
        viewMode="graph"
        onViewModeChange={vi.fn()}
        executionIdEditable={false}
        showCancelAction={false}
        showViewToggle={false}
      />
    );

    expect(screen.queryByRole("textbox")).toBeNull();
    expect(screen.getByText("ex-readonly")).toBeInTheDocument();
  });
});
