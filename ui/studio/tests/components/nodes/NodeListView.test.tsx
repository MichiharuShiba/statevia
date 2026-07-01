import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { NodeListView } from "../../../app/components/nodes/NodeListView";
import { renderWithUiText } from "../../testUtils";
import type { ExecutionNodeDTO } from "../../../app/lib/types";

const node = (id: string, status: ExecutionNodeDTO["status"]): ExecutionNodeDTO => ({
  executionNodeId: id,
  nodeType: "Task",
  status,
  attempt: 1,
  workerId: null,
  waitKey: null,
  canceledByExecution: false,
  stateName: `state-${id}`
});

describe("NodeListView", () => {
  it("ノード行クリックで選択コールバックを呼ぶ", () => {
    const onSelectNode = vi.fn();
    renderWithUiText(
      <NodeListView nodes={[node("n-1", "RUNNING")]} selectedNodeId={null} onSelectNode={onSelectNode} />
    );

    fireEvent.click(screen.getByText("n-1"));
    expect(onSelectNode).toHaveBeenCalledWith("n-1");
  });
});
