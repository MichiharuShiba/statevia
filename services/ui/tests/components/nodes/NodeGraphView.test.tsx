import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { NodeGraphView } from "../../../app/components/nodes/NodeGraphView";
import type { PositionedNode } from "../../../app/lib/graphLayout";
import type { MergedGraphNode } from "../../../app/lib/mergeGraph";
import { renderWithUiText } from "../../testUtils";

const positionedNode = (overrides: Partial<MergedGraphNode> = {}): PositionedNode<MergedGraphNode> => ({
  nodeId: "n-1",
  executionNodeId: "n-1",
  stateName: "task",
  nodeType: "Task",
  label: "Task 1",
  status: "RUNNING",
  attempt: 1,
  workerId: null,
  waitKey: null,
  canceledByExecution: false,
  x: 0,
  y: 0,
  w: 200,
  h: 80,
  ...overrides
});

describe("NodeGraphView", () => {
  it("ノードを描画しクリックで選択できる", async () => {
    const onSelectNode = vi.fn();
    const onResumeNode = vi.fn();

    renderWithUiText(
      <NodeGraphView
        nodes={[positionedNode()]}
        edges={[]}
        groups={[]}
        selectedNodeId={null}
        onSelectNode={onSelectNode}
        onResumeNode={onResumeNode}
        getResumeDisabledReason={() => null}
        heightClassName="h-[320px]"
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Task 1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Task 1"));
    expect(onSelectNode).toHaveBeenCalledWith("n-1");
  });
});
