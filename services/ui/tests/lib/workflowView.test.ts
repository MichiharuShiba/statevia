import { describe, expect, it } from "vitest";
import { buildWorkflowView } from "../../app/lib/workflowView";
import type { WorkflowDTO, WorkflowGraphDTO } from "../../app/lib/types";

describe("buildWorkflowView", () => {
  it("graph の conditionRouting を再評価せずそのまま保持する", () => {
    // Arrange
    const workflow: WorkflowDTO = {
      displayId: "ex-1",
      resourceId: "r-1",
      status: "Running",
      startedAt: "2026-01-01T00:00:00Z",
      cancelRequested: false,
      restartLost: false
    };
    const conditionRouting = {
      fact: "Completed",
      resolution: "matched_case",
      matchedCaseIndex: 0,
      caseEvaluations: [{ caseIndex: 0, matched: true }],
      evaluationErrors: []
    };
    const graph: WorkflowGraphDTO = {
      nodes: [
        {
          nodeId: "n-1",
          stateName: "RouteByScore",
          startedAt: "2026-01-01T00:00:00Z",
          completedAt: "2026-01-01T00:00:01Z",
          fact: "Completed",
          conditionRouting
        }
      ],
      edges: []
    };

    // Act
    const view = buildWorkflowView(workflow, graph);

    // Assert
    expect(view.nodes).toHaveLength(1);
    expect(view.nodes[0]?.conditionRouting).toEqual(conditionRouting);
  });
});
